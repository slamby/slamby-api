using Nest;
using Slamby.Elastic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Slamby.Common.DI;
using Slamby.Common.Config;
using Slamby.Common.Exceptions;
using Elasticsearch.Net;
using MoreLinq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    public class WordQuery : BaseQuery
    {

        /// <summary>
        /// this only get back the words but all words with maximum 1 occurence (multiple occurences will be skipped because of TermVectors Keys)
        /// </summary>
        /// <param name="client"></param>
        public WordQuery(ElasticClient client, SiteConfig siteConfig) : base(client, siteConfig) { }


        public IEnumerable<string> GetWords(string documentId, List<string> textFields, int ngramCount)
        {
            var fields = textFields.Select(f => $"{f}.{ngramCount}").ToArray();
            var elasticResponse = Client.TermVectors<DocumentElastic>(tv => tv
                    .Fields(fields)
                    .Id(documentId)
                    .TermStatistics(false)
                    .FieldStatistics(false)
                    .Positions(false)
                    .Offsets(false)
                    .Payloads(false)
                );
            ResponseValidator(elasticResponse);
            return elasticResponse.TermVectors.SelectMany(t => t.Value.Terms.Keys);
        }

        public Dictionary<string, Occurences> GetWordsWithOccurences(IEnumerable<string> documentIds, IEnumerable<string> textFields, int ngramCount)
        {
            var wordsDic = new Dictionary<string, Occurences>();
            if (!documentIds.Any())
            {
                return wordsDic;
            }

            var documentIdsList = documentIds.ToList();
            var fields = textFields.Select(f => $"{f}.{ngramCount}").ToArray();

            var wordTotalTermFreq = new Dictionary<string, Dictionary<string, int>>();
            var wordTermFreq = new Dictionary<string, Dictionary<string, int>>();

            foreach (var field in fields)
            {
                wordTotalTermFreq.Add(field, new Dictionary<string, int>());
                wordTermFreq.Add(field, new Dictionary<string, int>());
            }

            var noChance = false;
            var batchSize = SiteConfig.Resources.MaxIndexBulkCount;
            IMultiTermVectorsResponse elasticResponse = null;

            do
            {
                try
                {
                    var actualBatch = documentIdsList.Count > batchSize ? documentIdsList.Take(batchSize) : documentIdsList;
                    elasticResponse = Client.MultiTermVectors(mtv => mtv
                        .Fields(fields)
                        .GetMany<DocumentElastic>(actualBatch)
                        .TermStatistics(true)
                    );
                    ResponseValidator(elasticResponse);

                    var idsDic = actualBatch.ToDictionary(id => id, id => id);
                    documentIdsList.RemoveAll(id => idsDic.ContainsKey(id));

                    if (!elasticResponse.Documents.Any()) continue;

                    foreach (var field in fields)
                    {
                        var termVectors = elasticResponse.Documents
                            .Where(d => d.TermVectors.ContainsKey(field))
                            .Select(d => d.TermVectors[field])
                            .SelectMany(tv => tv.Terms)
                            .ToList();

                        foreach (var termVector in termVectors)
                        {
                            if (!wordTotalTermFreq[field].ContainsKey(termVector.Key))
                            {
                                wordTotalTermFreq[field].Add(termVector.Key, termVector.Value.TotalTermFrequency);
                            }

                            if (!wordTermFreq[field].ContainsKey(termVector.Key))
                            {
                                wordTermFreq[field].Add(termVector.Key, termVector.Value.TermFrequency);
                            }
                            else
                            {
                                wordTermFreq[field][termVector.Key] = wordTermFreq[field][termVector.Key] + termVector.Value.TermFrequency;
                            }
                        }
                    }
                }
                catch (Exception ex) 
                    when (ex is UnexpectedElasticsearchClientException || 
                          ex is ElasticSearchException)
                {
                    if (ex is UnexpectedElasticsearchClientException || 
                        elasticResponse.ServerError?.Status == 503 || 
                        elasticResponse.ServerError?.Status == 429)
                    {
                        if (batchSize == 1) noChance = true;
                        else
                        {
                            batchSize = Math.Max((Convert.ToInt32((double)batchSize / 2)), 1);
                            System.Threading.Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            while (documentIdsList.Any() || noChance);

            if (noChance) throw new OutOfResourceException("The server doesn't have enough resource to complete the request!");

            var allWordsList = wordTotalTermFreq.SelectMany(w => w.Value.Keys).Distinct().ToList();
            foreach (var word in allWordsList)
            {
                var wwo = new Occurences();
                foreach (var field in fields)
                {
                    if (wordTotalTermFreq[field].ContainsKey(word))
                    {
                        wwo.Corpus += wordTotalTermFreq[field][word];
                    }
                    if (wordTermFreq[field].ContainsKey(word))
                    {
                        wwo.Tag += wordTermFreq[field][word];
                    }
                }
                wordsDic.Add(word, wwo);
            }
            // because of the conat string in text field
            if (wordsDic.ContainsKey("")) wordsDic.Remove("");
            return wordsDic;
        }

        public Dictionary<string, Dictionary<string, Occurences>> GetWordsWithOccurencesByDocuments(IEnumerable<string> documentIds, IEnumerable<string> textFields, int ngramCount, int parallelLimit = -1)
        {
            var wordsDic = new Dictionary<string, Dictionary<string, Occurences>>();
            if (!documentIds.Any())
            {
                return wordsDic;
            }

            var documentIdsList = documentIds.ToList();
            var fields = textFields.Select(f => $"{f}.{ngramCount}").ToArray();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelLimit };

            var wordTotalTermFreq = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
            var wordTermFreq = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

            foreach (var documentId in documentIds)
            {
                wordTotalTermFreq.Add(documentId, new Dictionary<string, Dictionary<string, int>>());
                wordTermFreq.Add(documentId, new Dictionary<string, Dictionary<string, int>>());
                foreach (var field in fields)
                {
                    wordTotalTermFreq[documentId].Add(field, new Dictionary<string, int>());
                    wordTermFreq[documentId].Add(field, new Dictionary<string, int>());
                }
            }

            var noChance = false;
            var batchSize = SiteConfig.Resources.MaxIndexBulkCount;
            IMultiTermVectorsResponse elasticResponse = null;

            do
            {
                try
                {
                    // Needs ToList to create different list
                    var actualBatch = documentIdsList.Count > batchSize 
                        ? documentIdsList.Take(batchSize).ToList() 
                        : documentIdsList.ToList();

                    if (actualBatch.Count == 0) continue;

                    elasticResponse = Client.MultiTermVectors(mtv => mtv
                        .Fields(fields)
                        .GetMany<DocumentElastic>(actualBatch)
                        .TermStatistics(true)
                    );
                    ResponseValidator(elasticResponse);

                    var idsDic = actualBatch.ToDictionary(id => id, id => id);
                    documentIdsList.RemoveAll(id => idsDic.ContainsKey(id));

                    if (!elasticResponse.Documents.Any()) continue;

                    Parallel.ForEach(actualBatch, parallelOptions, documentId =>  
                    {
                        foreach (var field in fields)
                        {
                            var termVectors = elasticResponse.Documents
                                .Where(d => d.Id == documentId)
                                .Where(d => d.TermVectors.ContainsKey(field))
                                .Select(d => d.TermVectors[field])
                                .SelectMany(tv => tv.Terms)
                                .ToList();

                            foreach (var termVector in termVectors)
                            {
                                if (!wordTotalTermFreq[documentId][field].ContainsKey(termVector.Key))
                                {
                                    wordTotalTermFreq[documentId][field].Add(termVector.Key, termVector.Value.TotalTermFrequency);
                                }

                                if (!wordTermFreq[documentId][field].ContainsKey(termVector.Key))
                                {
                                    wordTermFreq[documentId][field].Add(termVector.Key, termVector.Value.TermFrequency);
                                }
                                else
                                {
                                    wordTermFreq[documentId][field][termVector.Key] = wordTermFreq[documentId][field][termVector.Key] + termVector.Value.TermFrequency;
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                    when (ex is UnexpectedElasticsearchClientException ||
                          ex is ElasticSearchException)
                {
                    if (ex is UnexpectedElasticsearchClientException ||
                        elasticResponse.ServerError?.Status == 503 ||
                        elasticResponse.ServerError?.Status == 429)
                    {
                        if (batchSize == 1) noChance = true;
                        else
                        {
                            batchSize = Math.Max((Convert.ToInt32((double)batchSize / 2)), 1);
                            System.Threading.Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
            while (documentIdsList.Any() || noChance);

            if (noChance) throw new OutOfResourceException("The server doesn't have enough resource to complete the request!");

            Parallel.ForEach(documentIds, parallelOptions, documentId =>
            {
                lock (wordsDic)
                {
                    wordsDic.Add(documentId, new Dictionary<string, Occurences>());
                }

                var allWordsList = wordTotalTermFreq[documentId].SelectMany(w => w.Value.Keys).Distinct().ToList();

                foreach (var word in allWordsList)
                {
                    if (string.IsNullOrEmpty(word)) continue;
                    var wwo = new Occurences();
                    foreach (var field in fields)
                    {
                        if (wordTotalTermFreq[documentId][field].ContainsKey(word))
                        {
                            wwo.Corpus += wordTotalTermFreq[documentId][field][word];
                        }
                        if (wordTermFreq[documentId][field].ContainsKey(word))
                        {
                            wwo.Tag += wordTermFreq[documentId][field][word];
                        }
                    }

                    lock (wordsDic)
                    {
                        wordsDic[documentId].Add(word, wwo);
                    }
                }
            });

            return wordsDic;
        }

        /// <summary>
        /// Get the Aggregated token count
        /// </summary>
        /// <param name="interpretedFields">DON'T PUT THE DOCUMENTELASTIC->TEXT FIELD HERE! RATHER THE INTERPRETEDFIELDS</param>
        /// <param name="ngramCount"></param>
        /// <param name="tagId">if it's null then the Aggregate will be run on all the DocumentElastic</param>
        /// <returns></returns>
        public int GetAllWordsOccurences(List<string> interpretedFields, int ngramCount, string tagField = null)
        {
            var desc = new SearchDescriptor<DocumentElastic>();
            var aggDesc = new AggregationContainerDescriptor<DocumentElastic>();
            foreach (var interpretedField in interpretedFields)
            {
                aggDesc.Sum(
                        interpretedField,
                        ad => ad.Field($"{interpretedField}.1{_tokenCountSuffix}"));
            }

            desc.Size(0);
            desc.Aggregations(a => aggDesc);

            desc.Query(q =>q.MatchAll());

            var response = Client.Search<DocumentElastic>(desc);
            ResponseValidator(response);

            // calculate the count for the current n-gram from the unigrams count
            var allCount = response.Aggregations.Sum(a => Convert.ToInt32(((ValueAggregate)a.Value).Value)) - (ngramCount - 1);
            return allCount;
        }

        /// <summary>
        /// Get the Aggregated token count per tag
        /// </summary>
        /// <param name="interpretedFields">DON'T PUT THE DOCUMENTELASTIC->TEXT FIELD HERE! RATHER THE INTERPRETEDFIELDS</param>
        /// <param name="ngramCount"></param>
        /// <param name="tagIds">the tagIds to run on</param>
        /// <param name="tagField"></param>
        /// <returns></returns>
        public Dictionary<string, int> CountForWord(List<string> interpretedFields, int ngramCount, List<string> tagIds, string tagField)
        {
            if (!tagIds.Any()) return new Dictionary<string, int>();
            var resultDic = new Dictionary<string, int>();
            foreach (var tagIdsBatch in tagIds.Batch(1000))
            {
                var mRequest = new MultiSearchRequest { Operations = new Dictionary<string, ISearchRequest>() };
                foreach (var tagId in tagIdsBatch)
                {
                    var desc = new SearchDescriptor<DocumentElastic>();
                    var aggDesc = new AggregationContainerDescriptor<DocumentElastic>();

                    foreach (var interpretedField in interpretedFields)
                    {
                        aggDesc.Sum(
                                interpretedField,
                                ad => ad.Field($"{interpretedField}.1{_tokenCountSuffix}"));
                    }

                    desc.Size(0);
                    desc.Aggregations(a => aggDesc);

                    if (!string.IsNullOrEmpty(tagId))
                    {
                        desc.Query(q => q.Term($"{tagField}", tagId));
                    }
                    else
                    {
                        desc.Query(q => q.MatchAll());
                    }

                    mRequest.Operations.Add(tagId, desc);
                }
                var result = Client.MultiSearch(mRequest);
                ResponseValidator(result);
                tagIdsBatch.ToList().ForEach(tid => resultDic.Add(tid, Convert.ToInt32(result.GetResponse<DocumentElastic>(tid).Aggregations.Sum(a => Convert.ToInt32(((ValueAggregate)a.Value).Value))) - (ngramCount - 1)));
            }
            return resultDic;
        }



        /*
         * The min_word_length not works properly (don't know why) so I have to do it an C#
        */
        /*
         * var elasticResponse = Client.LowLevel.Termvectors<TermVectorsResponse>(
                IndexName, 
                DocumentElastic.DocumentTypeName, 
                documentId,
                new PostData<object>(json));
            ((IBodyWithApiCallDetails)elasticResponse.Body).CallDetails = elasticResponse;
            ResponseValidator(elasticResponse.Body);
         * 
         * var json = GetMultiTermVectorsJson(actualBatch.ToList(), fields, true);
            elasticResponse = Client.LowLevel.Mtermvectors<MultiTermVectorsResponse>(
                IndexName,
                DocumentElastic.DocumentTypeName,
                new PostData<object>(json)
            );
            ((IBodyWithApiCallDetails)elasticResponse.Body).CallDetails = elasticResponse;
            ResponseValidator(elasticResponse.Body);
        /*private string GetTermVectorsJson(List<string> fields, bool needTermStatistics)
        {
            if (fields == null || fields.Count == 0) throw new Exception("Fields must be not empty!");
            return string.Format(
                "{{" +
                "\"filter\": {{\"min_word_length\": 1}}," +
                "\"positions\": \"false\"," +
                "\"term_statistics\": \"{0}\"," +
                "\"field_statistics\": \"false\"," +
                "\"positions\": \"false\"," +
                "\"offsets\": \"false\"," +
                "\"payloads\": \"false\"," +
                "\"fields\": [{1}]" +
                "}}", needTermStatistics, string.Join(",", fields.Select(f => $"\"{f}\""))
            );
        }

        private string GetMultiTermVectorsJson(List<string> ids, List<string> fields, bool needTermStatistics)
        {
            if (fields == null || fields.Count == 0) throw new Exception("Fields must be not empty!");
            if (ids == null || ids.Count == 0) throw new Exception("Ids must be not empty!");

            return string.Format(
                "{{" +
                    "\"parameters\": {{" +
                        "\"filter\": {{\"min_word_length\": 1}}," +
                        "\"positions\": \"false\"," +
                        "\"term_statistics\": \"{0}\"," +
                        "\"field_statistics\": \"false\"," +
                        "\"positions\": \"false\"," +
                        "\"offsets\": \"false\"," +
                        "\"payloads\": \"false\"," +
                        "\"fields\": [{1}]" +
                    "}}," + 
                    "\"ids\": [{2}]" + 
                "}}", needTermStatistics, string.Join(",", fields.Select(f => $"\"{f}\"")), string.Join(",", ids.Select(f => $"\"{f}\""))
            );
        }*/
    }
}
