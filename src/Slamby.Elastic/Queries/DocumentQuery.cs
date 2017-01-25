using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MoreLinq;
using Nest;
using Slamby.Common.DI;
using Slamby.Elastic.Models;
using Slamby.Common.Config;
using Elasticsearch.Net;

namespace Slamby.Elastic.Queries
{
    [TransientDependency(ServiceType = typeof(IDocumentQuery))]
    public class DocumentQuery : BaseQuery, IDocumentQuery
    {
        public const string SuggestName = "simple_suggest";

        public DocumentQuery(ElasticClient client, SiteConfig siteConfig) : base(client, siteConfig) { }

        public DocumentElastic Get(string id)
        {
            var sdesc = new SearchDescriptor<DocumentElastic>().Query(q => q.Ids(i => i.Values(id)));
            return Get(sdesc).Items.FirstOrDefault();
        }

        public IEnumerable<DocumentElastic> Get(IEnumerable<string> ids)
        {
            var sdesc = new SearchDescriptor<DocumentElastic>().Query(q => q.Ids(i => i.Values(ids)));
            return Get(sdesc).Items;
        }

        public SearchResult<DocumentElastic> GetAll()
        {
            var sdesc = new SearchDescriptor<DocumentElastic>();
            return Get(sdesc);
        }

        public IEnumerable<DocumentElastic> GetByTagId(string tagId, string tagField, IEnumerable<string> fields = null)
        {
            return GetByTagIds(new[] { tagId }, tagField, fields);
        }

        public IEnumerable<DocumentElastic> GetByTagIds(IEnumerable<string> tagIds, string tagField, IEnumerable<string> fields = null)
        {
            var field = MapDocumentObjectName(tagField);
            var sdesc = new SearchDescriptor<DocumentElastic>();
            var queryContDesc = new QueryContainerDescriptor<DocumentElastic>();
            var queryContainers = new List<QueryContainer>();

            foreach (var batchTagIds in tagIds.Batch(1000))
            {
                queryContainers.Add(queryContDesc
                    .Terms(selector => selector
                        .Terms(batchTagIds)
                        .Field(field)));
            }

            sdesc.Query(query => query
                .Bool(selector => selector
                    .Should(queryContainers.ToArray())));

            ApplyDocumentFieldFilter(sdesc, fields);

            return Get(sdesc).Items;
        }

        public ScrolledSearchResult<DocumentElastic> Filter(
            string generalQuery,
            IEnumerable<string> tagIds, string tagField,
            int limit,
            string orderBy, bool isDescending,
            IEnumerable<string> interPretedFields,
            IEnumerable<string> documentObjectFieldNames,
            IEnumerable<string> returningDocumentObjectFields,
            IEnumerable<string> ids = null,
            DateTime? dateStart = null,
            DateTime? dateEnd = null,
            string shouldQuery = null)
        {

            var sdesc = new SearchDescriptor<DocumentElastic>();
            var queryContDesc = new QueryContainerDescriptor<DocumentElastic>();
            var queryContainers = new List<QueryContainer>();

            if (!string.IsNullOrEmpty(generalQuery))
            {
                var modifiedQuery = generalQuery;

                //replace the field names (because of the document_object)
                if (documentObjectFieldNames?.Any() == true)
                {
                    modifiedQuery = PrefixQueryFields(modifiedQuery, documentObjectFieldNames);
                }
                if (interPretedFields != null && interPretedFields.Any())
                {
                    queryContainers.Add(
                        queryContDesc.QueryString(q => q
                            .Query(modifiedQuery)
                            .Fields(interPretedFields.Select(f => MapDocumentObjectName(f)).ToArray())));
                }
                else
                {
                    queryContainers.Add(
                        queryContDesc.QueryString(q => q.Query(modifiedQuery)));
                }
            }

            if (tagIds != null && tagIds.Any())
            {
                var shouldDesc = new BoolQueryDescriptor<DocumentElastic>();
                foreach (var batchTagIds in tagIds.Batch(1000))
                {
                    shouldDesc.Should(queryContDesc
                        .Terms(t => t
                            .Terms(batchTagIds)
                            .Field(MapDocumentObjectName(tagField))));
                }
                queryContainers.Add(queryContDesc.Bool(q => shouldDesc));
            }

            if (ids != null && ids.Any())
            {
                queryContainers.Add(queryContDesc.Ids(i => i.Values(ids)));
            }

            if (dateStart.HasValue)
            {
                queryContainers.Add(
                    queryContDesc.DateRange(d => d
                        .Field(DocumentElastic.ModifiedDateField)
                        .GreaterThan(dateStart.Value)));
            }
            if (dateEnd.HasValue)
            {
                queryContainers.Add(
                    queryContDesc.DateRange(d => d
                        .Field(DocumentElastic.ModifiedDateField)
                        .LessThanOrEquals(dateEnd.Value)));
            }

            // a REAL _should_ query, if we just add to the queryContainer then at least one of this condition must satisfied
            if (!string.IsNullOrEmpty(shouldQuery))
            {
                var modifiedQuery = shouldQuery;
                //replace the field names (because of the document_object)
                if (documentObjectFieldNames?.Any() == true)
                {
                    modifiedQuery = PrefixQueryFields(modifiedQuery, documentObjectFieldNames);
                }
                sdesc.Query(q => q.Bool(b => b
                    .Must(queryContainers.ToArray())
                    .Should(sq => sq.QueryString(qs => qs.Query(modifiedQuery)))));
            }
            else
            {
                sdesc.Query(q => q.Bool(b => b.Must(queryContainers.ToArray())));
            }

            if (!string.IsNullOrEmpty(orderBy))
            {
                var fieldName = MapDocumentObjectName(orderBy);
                if (interPretedFields != null && interPretedFields.Contains(orderBy))
                {
                    fieldName += ".raw";
                }

                sdesc.Sort(s => isDescending ? s.Descending(fieldName) : s.Ascending(fieldName));
            }
            
            if (limit >= 0)
            {
                sdesc.Size(limit);
            }

            ApplyDocumentFieldFilter(sdesc, returningDocumentObjectFields);

            return GetScrolled(sdesc);
        }

        public string PrefixQueryFields(string query, IEnumerable<string> documentObjectFieldNames)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (documentObjectFieldNames == null)
            {
                throw new ArgumentNullException(nameof(documentObjectFieldNames));
            }

            var prefixedQuery = query;
            var joinedFieldNames = string.Join("|", documentObjectFieldNames);

            prefixedQuery = new Regex($"(^(\\b{joinedFieldNames}\\b))([:\\.])")
                .Replace(prefixedQuery, $"{DocumentElastic.DocumentObjectMappingName}.$2$3");

            prefixedQuery = new Regex($"([^.])(\\b{joinedFieldNames}\\b)([:\\.])")
                .Replace(prefixedQuery, $"$1{DocumentElastic.DocumentObjectMappingName}.$2$3");

            return prefixedQuery;
        }

        public ScrolledSearchResult<DocumentElastic> GetScrolled(string scrollId)
        {
            var scrollDescriptor = new ScrollDescriptor<DocumentElastic>();

            scrollDescriptor.Scroll(new Time(5, TimeUnit.Minute));
            scrollDescriptor.ScrollId(scrollId);

            return GetScrolled(scrollDescriptor);
        }

        private SearchDescriptor<DocumentElastic> GetSampleDescriptor(string seed, IEnumerable<string> tagIds, string tagField)
        {
            var sdesc = new SearchDescriptor<DocumentElastic>();
            var queryContDesc = new QueryContainerDescriptor<DocumentElastic>();
            var queryContainers = new List<QueryContainer>();


            if (tagIds != null && tagIds.Any())
            {
                var shouldDesc = new BoolQueryDescriptor<DocumentElastic>();
                foreach (var batchTagIds in tagIds.Batch(1000))
                {
                    shouldDesc.Should(queryContDesc
                        .Terms(t => t
                            .Terms(batchTagIds)
                            .Field(MapDocumentObjectName(tagField))));
                }
                queryContainers.Add(queryContDesc.Bool(q => shouldDesc));
            }

            queryContainers.Add(
                queryContDesc.FunctionScore(f => f.Functions(fun => fun.RandomScore(seed)))
                );

            sdesc.Query(q => q.Bool(b => b.Must(queryContainers.ToArray())));

            return sdesc;
        }
        public SearchResult<DocumentElastic> Sample(string seed, IEnumerable<string> tagIds, string tagField, double percent,
            IEnumerable<string> fields = null)
        {
            var sdesc = GetSampleDescriptor(seed, tagIds, tagField);

            var total = Count(sdesc);
            var size = Convert.ToInt32(Math.Round(total * (percent / 100), 0));
            sdesc.Size(size);

            ApplyDocumentFieldFilter(sdesc, fields);

            var result = Get(sdesc);
            return result;
        }
        public SearchResult<DocumentElastic> Sample(string seed, IEnumerable<string> tagIds, string tagField, int size,
            IEnumerable<string> fields = null)
        {
            var sdesc = GetSampleDescriptor(seed, tagIds, tagField);
            sdesc.Size(size);

            ApplyDocumentFieldFilter(sdesc, fields);

            var result = Get(sdesc);
            return result;
        }

        public void Flush()
        {
            Client.Flush(IndexName);
        }

        public NestBulkResponse Index(DocumentElastic documentElastic)
        {
            return Index(new List<DocumentElastic> { documentElastic });
        }

        public NestBulkResponse Index(IEnumerable<DocumentElastic> documentElastics, bool doFlush = true)
        {
            return base.Index(documentElastics, doFlush);
        }
        
        public NestBulkResponse ParallelBulkIndex(IEnumerable<DocumentElastic> documentElastics, int parallelLimit, decimal objectsSizeInBytes)
        {
            var response = base.ParallelBulkIndex(documentElastics, parallelLimit, objectsSizeInBytes);
            return response;
        }

        public NestBulkResponse ParallelBulkIndex(IEnumerable<DocumentElastic> documentElastics, int parallelLimit)
        {
            var response = base.ParallelBulkIndex(documentElastics, parallelLimit);
            return response;
        }

        public string Update(string id, DocumentElastic documentElastic)
        {
            var response = Client.Update(new DocumentPath<DocumentElastic>(id), ur => ur.Doc(documentElastic));
            ResponseValidator(response);
            ResponseValidator(Client.Flush(IndexName));
            return response.Id;
        }

        public void Delete(string id)
        {
            Delete(new List<string> { id });
        }

        public void Delete(IEnumerable<string> ids)
        {
            if (!ids.Any()) return;
            var deleteResponse = Client.DeleteMany<DocumentElastic>(ids.Select(id => new DocumentElastic { Id = id }));
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
        }

        public long CountAll()
        {
            return Client.Count<DocumentElastic>().Count;
        }

        public Dictionary<string, long> CountAll(List<string> indexNames)
        {
            var resultDic = new Dictionary<string, long>();
            if (!indexNames.Any()) return resultDic;

            var mRequest = new MultiSearchRequest { Operations = new Dictionary<string, ISearchRequest>() };
            foreach (var indexName in indexNames)
            {
                var sr = new SearchRequest(indexName);
                mRequest.Operations.Add(indexName, new SearchRequest<DocumentElastic>(indexName) { Query = new MatchAllQuery(), Size = 0 });
            }
            var result = Client.MultiSearch(mRequest);
            ResponseValidator(result);
            
            indexNames.ForEach(indexName => resultDic.Add(indexName, Convert.ToInt32(result.GetResponse<DocumentElastic>(indexName).Total)));
            return resultDic;
        }

        public long Count(string tagId = null, string tagField = null)
        {
            var sdesc = new SearchDescriptor<DocumentElastic>();
            if (tagId == null) return Count(sdesc);
            else return CountForTags(new List<string> { tagId }, tagField)[tagId];
        }

        public Dictionary<string, int> CountForTags(List<string> tagIds, string tagField)
        {
            if (!tagIds.Any()) return new Dictionary<string, int>();
            var resultDic = new Dictionary<string, int>();
            foreach (var tagIdsBatch in tagIds.Batch(1000))
            {
                var mRequest = new MultiSearchRequest { Operations = new Dictionary<string, ISearchRequest>() };
                foreach (var tagId in tagIdsBatch)
                {
                    var termQuery = new TermQuery
                    {
                        Field = MapDocumentObjectName(tagField),
                        Value = tagId
                    };
                    mRequest.Operations.Add(tagId, new SearchRequest<DocumentElastic> { Query = new QueryContainer(termQuery), Size = 0 });
                }
                var result = Client.MultiSearch(mRequest);
                ResponseValidator(result);
                tagIdsBatch.ToList().ForEach(tid => resultDic.Add(tid, Convert.ToInt32(result.GetResponse<DocumentElastic>(tid).Total)));
            }
            return resultDic;
        }

        public bool IsExists(string id)
        {
            return Client.DocumentExists<DocumentElastic>(id).Exists;
        }

        public SearchResult<DocumentElastic> GetTagIdFieldOnly(string tagField)
        {
            var sdesc = new SearchDescriptor<DocumentElastic>();
            sdesc.Source(s => s.Include(i => i.Fields(MapDocumentObjectName(tagField))));

            return Get(sdesc);
        }

        public void Optimize()
        {
            Client.Optimize(Indices.Index(IndexName), od => od.WaitForMerge(false).OnlyExpungeDeletes());
        }

        public Dictionary<string, List<string>> GetExistsForQueries(Dictionary<string, string> queries, List<string> ids)
        {
            if (ids == null) throw new Exception("Ids must not be null!");
            var queryContDesc = new QueryContainerDescriptor<DocumentElastic>();
            var msDesc = new MultiSearchDescriptor();

            foreach (var queryKvp in queries)
            {
                var queryContainers = new List<QueryContainer>();
                queryContainers.Add(queryContDesc.Ids(i => i.Values(ids)));
                queryContainers.Add(queryContDesc.QueryString(q => q.Query(queryKvp.Value)));

                var sdesc = new SearchDescriptor<DocumentElastic>();
                sdesc.Query(q => q.Bool(b => b.Must(queryContainers.ToArray())));
                sdesc.Size(ids.Count);

                msDesc.Search<DocumentElastic>(queryKvp.Key, s => sdesc);
            }
            var resp = Client.MultiSearch(ms => msDesc);
            ResponseValidator(resp);
            return queries.Keys.ToDictionary(q => q, q => resp.GetResponse<DocumentElastic>(q).Hits.Select(h => h.Id).ToList());
        }

        public static string MapDocumentObjectName(string fieldName)
        {
            // If it is already prefixed
            if (fieldName.StartsWith($"{DocumentElastic.DocumentObjectMappingName}.", StringComparison.Ordinal))
            {
                return fieldName;
            }

            return string.Format("{0}.{1}", DocumentElastic.DocumentObjectMappingName, fieldName);
        }

        /// <summary>
        /// Returns with DocumentElastic own fields + parameter fields prefixed with DocumentObjectMappingName
        /// </summary>
        /// <param name="documentObjectFields"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetDocumentElasticFields(IEnumerable<string> documentObjectFields)
        {
            var prefixedDocumentObjectFields = documentObjectFields.Select(MapDocumentObjectName);
            var documentElasticOwnFields = DocumentElastic.OwnFields.Split(',');
            var fields = documentElasticOwnFields.Union(prefixedDocumentObjectFields);

            return fields;
        }

        private void ApplyDocumentFieldFilter<T>(SearchDescriptor<T> sdesc, IEnumerable<string> documentObjectFields) where T : class
        {
            var fields = GetDocumentElasticFields(documentObjectFields).ToArray();

            sdesc.Source(desc => desc
               .Include(f => f
                   .Fields(fields)));
        }

        

        public ISearchResponse<DocumentElastic> Search(
            AutoCompleteSettingsElastic autoCompleteSettings, 
            SearchSettingsElastic searchSettings,
            string text,
            IEnumerable<string> documentObjectFieldNames,
            string tagField,
            IEnumerable<string> interPretedFields,
            FilterElastic defaultFilter,
            List<WeightElastic> defaultWeights
            )
        {

            var sdesc = new SearchDescriptor<DocumentElastic>();

            #region SEARCH

            if (searchSettings?.Count > 0)
            {
                var queryContDesc = new QueryContainerDescriptor<DocumentElastic>();
                var queryContainers = new List<QueryContainer>();

                var searchFields = searchSettings.SearchFieldList.Select(f => MapDocumentObjectName(f)).ToArray();
                //FILTER
                if (searchSettings.UseDefaultFilter)
                {
                    if (!string.IsNullOrEmpty(defaultFilter?.Query))
                    {
                        var modifiedQuery = documentObjectFieldNames?.Any() == true ?
                            PrefixQueryFields(defaultFilter.Query, documentObjectFieldNames) :
                            defaultFilter.Query;
                        queryContainers.Add(queryContDesc.QueryString(q => q.Query(modifiedQuery)));
                    }
                    if (defaultFilter?.TagIdList?.Any() == true)
                    {
                        var shouldDesc = new BoolQueryDescriptor<DocumentElastic>();
                        foreach (var batchTagIds in defaultFilter.TagIdList.Batch(1000))
                        {
                            shouldDesc.Should(queryContDesc
                                .Terms(t => t
                                    .Terms(batchTagIds)
                                    .Field(MapDocumentObjectName(tagField))));
                        }
                        queryContainers.Add(queryContDesc.Bool(q => shouldDesc));
                    }
                }
                if (!string.IsNullOrEmpty(searchSettings.Filter?.Query))
                {
                    var modifiedQuery = documentObjectFieldNames?.Any() == true ?
                        PrefixQueryFields(searchSettings.Filter.Query, documentObjectFieldNames) :
                        searchSettings.Filter.Query;
                    queryContainers.Add(queryContDesc.QueryString(q => q.Query(modifiedQuery)));
                }
                if (searchSettings.Filter?.TagIdList?.Any() == true)
                {
                    var shouldDesc = new BoolQueryDescriptor<DocumentElastic>();
                    foreach (var batchTagIds in searchSettings.Filter.TagIdList.Batch(1000))
                    {
                        shouldDesc.Should(queryContDesc
                            .Terms(t => t
                                .Terms(batchTagIds)
                                .Field(MapDocumentObjectName(tagField))));
                    }
                    queryContainers.Add(queryContDesc.Bool(q => shouldDesc));
                }

                // MATCH TYPE SEARCH
                if (searchSettings.Type == (int)SDK.Net.Models.Enums.SearchTypeEnum.Match)
                {
                    var mqd = new MultiMatchQueryDescriptor<DocumentElastic>()
                        .Query(text)
                        .Type(TextQueryType.BestFields)
                        .CutoffFrequency(searchSettings.CutOffFrequency)
                        .Fuzziness(searchSettings.Fuzziness < 0 ? Fuzziness.Auto : Fuzziness.EditDistance(searchSettings.Fuzziness))
                        .Fields(f => f.Fields(searchFields))
                        .Operator((Operator)searchSettings.Operator);
                    queryContainers.Add(queryContDesc.MultiMatch(q => mqd));
                }

                // QUERY(STRING) TYPE SEARCH
                if (searchSettings.Type == (int)SDK.Net.Models.Enums.SearchTypeEnum.Query)
                {
                    var modifiedQuery = documentObjectFieldNames?.Any() == true ?
                        PrefixQueryFields(text, documentObjectFieldNames) :
                        text;
                    var qsd = new QueryStringQueryDescriptor<DocumentElastic>()
                        .Query(text)
                        //cutoff_frequency is not supported for querystring query
                        //.CutoffFrequency(searchSettings.CutOffFrequency)
                        .Fuzziness(searchSettings.Fuzziness < 0 ? Fuzziness.Auto : Fuzziness.EditDistance(searchSettings.Fuzziness))
                        .Fields(f => f.Fields(searchFields))
                        .DefaultOperator((Operator)searchSettings.Operator);
                    queryContainers.Add(queryContDesc.QueryString(q => qsd));
                }

                // WEIGHTS
                // a REAL _should_ query, if we just add to the queryContainer then at least one of this condition must satisfied
                var weights = new List<WeightElastic>();
                if (searchSettings.UseDefaultWeights && (defaultWeights?.Any() == true))
                {
                    weights.AddRange(defaultWeights);
                }
                if (searchSettings.Weights?.Any() == true)
                {
                    weights.AddRange(searchSettings.Weights);
                }
                if (weights.Any())
                {
                    var shouldQuery = string.Join(" ", weights.Select(k => $"({k.Query})^{k.Value}"));
                    var modifiedQuery = documentObjectFieldNames?.Any() == true ?
                        PrefixQueryFields(shouldQuery, documentObjectFieldNames) :
                        shouldQuery;
                    sdesc.Query(q => q.Bool(b => b
                        .Must(queryContainers.ToArray())
                        .Should(sq => sq.QueryString(qs => qs.Query(modifiedQuery)))));
                }
                else
                {
                    sdesc.Query(q => q.Bool(b => b.Must(queryContainers.ToArray())));
                }

                // ORDER
                if (!string.IsNullOrEmpty(searchSettings.Order?.OrderByField))
                {
                    var fieldName = MapDocumentObjectName(searchSettings.Order.OrderByField);
                    if (interPretedFields != null && interPretedFields.Contains(searchSettings.Order.OrderByField))
                    {
                        fieldName += ".raw";
                    }
                    sdesc.Sort(s => (int)SortOrder.Descending == searchSettings.Order.OrderDirection ? s.Descending(fieldName) : s.Ascending(fieldName));
                }

                // COUNT
                sdesc.Size(searchSettings.Count);

                ApplyDocumentFieldFilter(sdesc, searchSettings.ResponseFieldList.Select(f => MapDocumentObjectName(f)));
            }
            else
            {
                sdesc.Size(0);
            }
            
            #endregion


            #region SUGGEST

            if (autoCompleteSettings?.Count > 0)
            {
                var psgd = new PhraseSuggesterDescriptor<DocumentElastic>()
                .Field(DocumentElastic.TextField)
                .Size(autoCompleteSettings.Count)
                //.RealWordErrorLikelihood(0.95), 0.95 is the default value
                .Confidence(autoCompleteSettings.Confidence)
                .MaxErrors(autoCompleteSettings.MaximumErrors)
                .DirectGenerator(dg => dg
                    .Field(DocumentElastic.TextField)
                    .SuggestMode(SuggestMode.Always)
                    .MinWordLength(3)
                    .MinDocFrequency(3)
                    .Size(1))
                .Collate(c => c
                    .Prune()
                    .Query(q => q
                    //unfortunately Params is not working here so had to hack the text field like this
                      .Inline($"{{\"match\": {{\"{DocumentElastic.TextField}\" : {{\"query\": \"{{{{suggestion}}}}\", \"operator\": \"and\"}}}}}}")
                    )
                )
                .Text(text);
                sdesc.Suggest(s => s.Phrase(SuggestName, p => psgd));
            }
            

            #endregion


            var resp = Client.Search<DocumentElastic>(sdesc);
            ResponseValidator(resp);

            return resp;
        }
    }
}
