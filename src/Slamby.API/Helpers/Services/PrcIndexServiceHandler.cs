using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.Cerebellum;
using Slamby.Cerebellum.Dictionary;
using Slamby.Cerebellum.Scorer;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using Slamby.Common;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class PrcIndexServiceHandler
    {
        readonly ServiceQuery serviceQuery;
        readonly PrcIndexRedisHandler redisHandler;
        readonly IQueryFactory queryFactory;
        readonly ParallelService parallelService;
        readonly ILogger<PrcIndexServiceHandler> logger;
        readonly ProcessHandler processHandler;
        readonly IGlobalStoreManager GlobalStore;

        const double ScoreMultiplier = 1.7;

        public PrcIndexServiceHandler(ServiceQuery serviceQuery, PrcIndexRedisHandler prcIndexRedisHandler, IQueryFactory queryFactory,
            ParallelService parallelService, ILogger<PrcIndexServiceHandler> logger, ProcessHandler processHandler, IGlobalStoreManager globalStore)
        {
            this.GlobalStore = globalStore;
            this.processHandler = processHandler;
            this.logger = logger;
            this.parallelService = parallelService;
            this.queryFactory = queryFactory;
            this.redisHandler = prcIndexRedisHandler;
            this.serviceQuery = serviceQuery;
        }

        public void CleanPrcIndex(string serviceId)
        {
            try
            {
                var settings = serviceQuery.GetSettings<PrcSettingsElastic>(serviceId);
                settings.IndexSettings = null;
                serviceQuery.IndexSettings(settings);

                redisHandler.Clean(PrcIndexRedisKey.ServiceDeleteKey(serviceId));
            }
            catch (Exception ex)
            {
                logger.LogError($"Unable to clean PRC Index {serviceId}", ex);
            }
        }

        public void Index(string processId, PrcSettingsElastic prcSettings, CancellationToken token)
        {
            var logPrefix = $"Prc Index {processId}";

            try
            {
                const int parallelMultiplier = 2;
                var service = serviceQuery.Get(prcSettings.ServiceId);

                redisHandler.Clean(PrcIndexRedisKey.ServiceDeleteKey(prcSettings.ServiceId));

                var wordQuery = queryFactory.GetWordQuery(prcSettings.DataSetName);
                var lockObject = new object();

                var documentQuery = queryFactory.GetDocumentQuery(prcSettings.DataSetName);
                var tagsWithDocumentCounts = documentQuery.CountForTags(prcSettings.IndexSettings.FilterTagIdList, GlobalStore.DataSets.Get(prcSettings.DataSetName).DataSet.TagField);
                var allDocumentsCount = tagsWithDocumentCounts.Sum(d => d.Value);

                logger.LogInformation($"{logPrefix} starts with ParallelLimit: {parallelService.ParallelLimit * parallelMultiplier}, Tags Count: {prcSettings.IndexSettings.FilterTagIdList.Count}, All documents count: {allDocumentsCount}");

                var allDocProgress = new Progress(allDocumentsCount);

                // lekérjük az aktuális tag-hez(és Filter - hez) tartozó doksikat (elastic - ból).
                foreach (var tagId in prcSettings.IndexSettings.FilterTagIdList)
                {
                    if (token.IsCancellationRequested)
                    {
                        CleanPrcIndex(prcSettings.ServiceId);
                        processHandler.Cancelled(processId);
                        logger.LogInformation($"{logPrefix} cancelled Tag: `{tagId}`");
                        return;
                    }

                    logger.LogTrace($"{logPrefix} preparing Tag: `{tagId}`");

                    var globalSubset = GlobalStore.ActivatedPrcs.Get(prcSettings.ServiceId).PrcSubsets[tagId];
                    if (globalSubset.WordsWithOccurences == null)
                    {
                        continue;
                    }

                    var documentElasticIds = GetDocumentIds(prcSettings.DataSetName, prcSettings.IndexSettings.FilterQuery, new List<string> { tagId }, null, prcSettings.IndexSettings.IndexDate).OrderBy(o => o).ToList();
                    if (documentElasticIds.Count == 0)
                    {
                        continue;
                    }

                    var wwoDocuments = wordQuery.GetWordsWithOccurencesByDocuments(
                        documentElasticIds,
                        prcSettings.FieldsForRecommendation.Select(DocumentQuery.MapDocumentObjectName),
                        1, parallelLimit: parallelService.ParallelLimit);
                    var cleanedTextDocuments = wwoDocuments.ToDictionary(w => w.Key, w => GetCleanedText(w.Value));

                    var docProgress = new Progress(documentElasticIds.Count);
                    Parallel.ForEach(documentElasticIds, parallelService.ParallelOptions(parallelMultiplier), (documentId, loopState) =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }

                        try
                        {
                            logger.LogTrace($"{logPrefix} preparing Document: `{documentId}`/`{tagId}`");

                            // kiszámoljuk az aktuális doksi base dictionary - jét
                            var scorer = GetScorer(globalSubset, wwoDocuments[documentId], cleanedTextDocuments[documentId], GlobalStore.ActivatedPrcs.Get(prcSettings.ServiceId).PrcScorers[tagId]);
                            if (scorer == null)
                            {
                                return;
                            }

                            var similarDocuments = new List<KeyValuePair<string, double>>();

                            // documentElastics except document 
                            foreach (var siblingDocumentId in documentElasticIds)
                            {
                                if (siblingDocumentId == documentId) continue;
                                var wwoSibling = wwoDocuments[siblingDocumentId];
                                if (wwoSibling.Keys.Intersect(scorer.BaseDic.Keys).Count() == 0) continue;

                                var finalScore = GetPrcScore(scorer, cleanedTextDocuments[siblingDocumentId]);
                                if (finalScore > 0)
                                {
                                    similarDocuments.Add(new KeyValuePair<string, double>(siblingDocumentId, finalScore));
                                }
                            }

                            var redisKey = new PrcIndexRedisKey(prcSettings.ServiceId, tagId, documentId);
                            redisHandler.AddDocuments(redisKey, similarDocuments);
                        }
                        finally
                        {
                            logger.LogTrace($"{logPrefix} prepared Document: `{documentId}`/`{tagId}`");

                            allDocProgress.Step();
                            var value = docProgress.Step();
                            if (value % 50 == 0)
                            {
                                lock (lockObject)
                                {
                                    processHandler.Changed(processId, allDocProgress.Percent.Round(6));
                                }

                                logger.LogTrace($"{logPrefix} progress {docProgress} in `{tagId}`");
                                logger.LogTrace($"{logPrefix} total progress is {allDocProgress}");
                            }
                            if (value % 1000 == 0)
                            {
                                GC.Collect();
                            }
                        }
                    });

                    if (token.IsCancellationRequested)
                    {
                        CleanPrcIndex(prcSettings.ServiceId);
                        processHandler.Cancelled(processId);
                        logger.LogInformation($"{logPrefix} cancelled Tag: `{tagId}`");
                        return;
                    }

                    logger.LogInformation($"{logPrefix} prepared Tag: `{tagId}`");
                    logger.LogInformation($"{logPrefix} total progress is {allDocProgress}");

                    GC.Collect();
                }

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyIndexed_0_Service_1, ServiceTypeEnum.Prc, service.Name));
                logger.LogInformation($"{logPrefix} finished");
            }
            catch (Exception ex)
            {
                logger.LogError($"{logPrefix} failed. {ex.Message} {ex.StackTrace}");
                CleanPrcIndex(prcSettings.ServiceId);
                processHandler.Interrupted(processId, ex);
            }
            finally
            {
                GC.Collect();
            }
        }

        public void IndexPartial(string processId, PrcSettingsElastic prcSettings, CancellationToken token)
        {
            var logPrefix = $"Prc Partial Index {processId}";

            try
            {
                const int parallelMultiplier = 2;
                var partialIndexDate = DateTime.UtcNow;
                var service = serviceQuery.Get(prcSettings.ServiceId);

                var wordQuery = queryFactory.GetWordQuery(prcSettings.DataSetName);
                var tagProgress = new Progress(prcSettings.IndexSettings.FilterTagIdList.Count);

                logger.LogInformation($"{logPrefix} starts with ParallelLimit: {parallelService.ParallelLimit * parallelMultiplier}, Tags Count: {prcSettings.IndexSettings.FilterTagIdList.Count}");

                //lekérjük a legutóbbi indexelés óta módosult vagy létrehozott doksikat(Filter - t figyelve)
                //TODO get IndexFilterTagIds which has changed only
                foreach (var tagId in prcSettings.IndexSettings.FilterTagIdList)
                {
                    if (token.IsCancellationRequested)
                    {
                        processHandler.Cancelled(processId);
                        logger.LogInformation($"{logPrefix} cancelled Tag: `{tagId}`");
                        return;
                    }

                    var changedDocumentElasticIds = GetDocumentIds(prcSettings.DataSetName, prcSettings.IndexSettings.FilterQuery, new List<string> { tagId }, prcSettings.IndexSettings.IndexDate, partialIndexDate).OrderBy(o => o);
                    // no changed document found since last index
                    if (!changedDocumentElasticIds.Any())
                    {
                        continue;
                    }

                    // kiszámoljuk az aktuális doksi base dictionary - jét
                    var globalSubset = GlobalStore.ActivatedPrcs.Get(prcSettings.ServiceId).PrcSubsets[tagId];
                    if (globalSubset.WordsWithOccurences == null)
                    {
                        continue;
                    }

                    // Kellenek azok a doksik is az indexeléshez amik nem változtak
                    var documentElasticIds = GetDocumentIds(prcSettings.DataSetName, prcSettings.IndexSettings.FilterQuery, new List<string> { tagId }, dateEnd: partialIndexDate);
                    if (!documentElasticIds.Any())
                    {
                        continue;
                    }

                    var wwoDocuments = wordQuery.GetWordsWithOccurencesByDocuments(
                        documentElasticIds,
                        prcSettings.FieldsForRecommendation.Select(DocumentQuery.MapDocumentObjectName),
                        1, parallelLimit: parallelService.ParallelLimit);
                    var cleanedTextDocuments = wwoDocuments.ToDictionary(w => w.Key, w => GetCleanedText(w.Value));

                    Parallel.ForEach(changedDocumentElasticIds, parallelService.ParallelOptions(parallelMultiplier), (documentId, loopState) =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }
                        try
                        {
                            logger.LogTrace($"{logPrefix} preparing Document: `{documentId}`/`{tagId}`");

                            var scorer = GetScorer(globalSubset, wwoDocuments[documentId], cleanedTextDocuments[documentId], GlobalStore.ActivatedPrcs.Get(prcSettings.ServiceId).PrcScorers[tagId]);
                            if (scorer == null)
                            {
                                return;
                            }

                            var similarDocuments = new List<KeyValuePair<string, double>>();

                            // calculate documentElastics scores except current document 
                            foreach (var siblingDocumentId in documentElasticIds)
                            {
                                if (siblingDocumentId == documentId) continue;
                                var wwoSibling = wwoDocuments[siblingDocumentId];
                                if (!wwoSibling.Any(w => scorer.BaseDic.Keys.Contains(w.Key)))
                                {
                                    continue;
                                }

                                var finalScore = GetPrcScore(scorer, cleanedTextDocuments[siblingDocumentId]);
                                if (finalScore > 0)
                                {
                                    similarDocuments.Add(new KeyValuePair<string, double>(siblingDocumentId, finalScore));
                                }
                            }

                            var redisKey = new PrcIndexRedisKey(prcSettings.ServiceId, tagId, documentId);
                            var indexedDocumentIds = redisHandler.GetDocuments(redisKey);
                            var unchangedDocumentIds = indexedDocumentIds
                                .Where(idx => similarDocuments.Any(sim => sim.Key == idx.Element && sim.Value == idx.Score))
                                .Select(s => s.Element)
                                .ToList();

                            var documentIdsToAdjust = indexedDocumentIds.Select(s => s.Element.ToString())
                                .Union(similarDocuments.Select(s => s.Key))
                                .Where(w => !unchangedDocumentIds.Contains(w))
                                .Distinct()
                                .ToList();

                            redisHandler.ReplaceDocuments(redisKey, similarDocuments);

                            //ezekre doksikra (ha már kéznél vannak), visszafelé is kiszámoljuk a prc score-t
                            foreach (var reverseDocumentId in documentIdsToAdjust)
                            {
                                var reverseRedisKey = new PrcIndexRedisKey(prcSettings.ServiceId, tagId, reverseDocumentId);
                                redisHandler.RemoveDocument(reverseRedisKey, documentId);

                                // ha van egyezőség kiszámolni a prcscore-t
                                // és beszúrni a redisbe
                                var reverseScorer = GetScorer(globalSubset, wwoDocuments[reverseDocumentId], cleanedTextDocuments[reverseDocumentId], GlobalStore.ActivatedPrcs.Get(prcSettings.ServiceId).PrcScorers[tagId]);
                                if (reverseScorer == null)
                                {
                                    continue;
                                }

                                var wwoReverse = wwoDocuments[documentId];
                                if (wwoReverse.Keys.Intersect(reverseScorer.BaseDic.Keys).Count() > 0)
                                {
                                    var finalScore = GetPrcScore(reverseScorer, cleanedTextDocuments[documentId]);
                                    if (finalScore > 0)
                                    {
                                        redisHandler.AddDocument(reverseRedisKey, documentId, finalScore);
                                    }
                                }

                                // levágjuk a max listaelemszám felettieket
                                redisHandler.TrimDocuments(reverseRedisKey);
                            }
                        }
                        finally
                        {
                            logger.LogTrace($"{logPrefix} prepared Document: `{documentId}`/`{tagId}`");
                        }
                    });

                    if (token.IsCancellationRequested)
                    {
                        processHandler.Cancelled(processId);
                        logger.LogInformation($"{logPrefix} cancelled Tag: `{tagId}`");
                        return;
                    }

                    tagProgress.Step();
                    processHandler.Changed(processId, tagProgress.Percent.Round(6));

                    GC.Collect();
                }

                prcSettings.IndexSettings.IndexDate = partialIndexDate;
                serviceQuery.IndexSettings(prcSettings);

                logger.LogInformation($"{logPrefix} finished");
                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyPartialIndexed_0_Service_1, ServiceTypeEnum.Prc, service.Name));
            }
            catch (Exception ex)
            {
                processHandler.Interrupted(processId, ex);
            }
            finally
            {
                GC.Collect();
            }
        }

        public IEnumerable<PrcRecommendationResult> RecommendById(string id, PrcSettingsElastic prcSettings, PrcRecommendationByIdRequest request)
        {
            var result = new List<PrcRecommendationResult>();
            var globalStoreDataSet = GlobalStore.DataSets.Get(prcSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;

            var documentQuery = queryFactory.GetDocumentQuery(dataSet.Name);
            var fieldsForRecommendation = prcSettings.FieldsForRecommendation;

            var filterOrWeight = !string.IsNullOrEmpty(request.Query) || request?.Weights?.Any() == true;

            var tagId = string.Empty;
            if (string.IsNullOrEmpty(request.TagId))
            {
                var documentElastic = documentQuery.Get(request.DocumentId);
                if (documentElastic == null) return result;
                var tagToken = JTokenHelper.GetToken(documentElastic.DocumentObject).GetPathToken(dataSet.TagField);
                tagId = JTokenHelper.GetUnderlyingToken(tagToken)?.ToString();
                if (tagId == null) return result;
            }
            else
            {
                tagId = request.TagId;
            }

            var similarDocIdsWithScore = redisHandler.GetTopNDocuments(new PrcIndexRedisKey(id, tagId, request.DocumentId), filterOrWeight ? -1 : request.Count - 1);

            Dictionary<string, double> resultDictionary = similarDocIdsWithScore;

            var documentElastics = (filterOrWeight || request.NeedDocumentInResult) ?
                                    GetDocuments(dataSet.Name, request.Query, null, fieldsForRecommendation, similarDocIdsWithScore.Keys, request.NeedDocumentInResult) : 
                                    null;
            // ha a Filter és a Weights is üres, a TOP Count doksi Id - t visszaadjuk score-jaikkal. (ha kell a document is, akkor elastic - tól elkérjük ezeket pluszban)
            if (filterOrWeight)
            {
                // ezekre a doksikra módosítjuk a prc score - t a Weights-el
                var docIdsWithScore = documentElastics.ToDictionary(k => k.Id, v => similarDocIdsWithScore[v.Id]);

                //súlyozás
                if (request?.Weights?.Any() == true)
                {
                    var weightsDic = request.Weights.ToDictionary(key => Guid.NewGuid().ToString(), value => value);

                    var docIds = docIdsWithScore.Keys.ToList();
                    var queries = weightsDic.ToDictionary(key => key.Key, value => documentQuery.PrefixQueryFields(value.Value.Query, globalStoreDataSet.DocumentFields));
                    var ids = documentQuery.GetExistsForQueries(queries, docIds).ToDictionary(k => k.Key, v => v.Value.ToDictionary(ke => ke, va => va));

                    var allWeightsCount = request.Weights.Count;
                    foreach (var docId in docIds)
                    {
                        var weightsSum = weightsDic.Where(w => ids[w.Key].ContainsKey(docId)).Sum(w => w.Value.Value);
                        var pow = 1 + (weightsSum / allWeightsCount);
                        var score = Math.Pow(docIdsWithScore[docId] + 1, pow) - 1;
                        docIdsWithScore[docId] = score;
                    }
                }

                resultDictionary = docIdsWithScore;
            }

            var recommendation = resultDictionary
                .OrderByDescending(o => o.Value)
                .Take(request.Count)
                .Select(s => new PrcRecommendationResult()
                {
                    DocumentId = s.Key,
                    Score = s.Value,
                    Document = request.NeedDocumentInResult ? documentElastics.SingleOrDefault(d => d.Id == s.Key).DocumentObject : null
                });

            return recommendation;
        }

        private List<DocumentElastic> GetDocuments(string dataSetName, string query, IEnumerable<string> tagIds,
            IEnumerable<string> fieldsForRecommendation, IEnumerable<string> documentIds, bool needDocumentInResult)
        {
            if (!documentIds.Any()) return new List<DocumentElastic>();
            var globalStoreDataSet = GlobalStore.DataSets.Get(dataSetName);
            var dataSet = globalStoreDataSet.DataSet;
            var documentQuery = queryFactory.GetDocumentQuery(dataSetName);

            var documentElastics = new List<DocumentElastic>();

            var scrollResult = documentQuery
                .Filter(query,
                        tagIds,
                        dataSet.TagField,
                        -1, null, false,
                        fieldsForRecommendation,
                        globalStoreDataSet.DocumentFields,
                        DocumentService.GetFieldFilter(globalStoreDataSet, new List<string> { needDocumentInResult ? "*" : globalStoreDataSet.DataSet.IdField }),
                        documentIds);

            documentElastics.AddRange(scrollResult.Items);

            while (scrollResult.Items.Any())
            {
                scrollResult = documentQuery.GetScrolled(scrollResult.ScrollId);
                documentElastics.AddRange(scrollResult.Items);
            }

            return documentElastics;
        }

        private List<string> GetDocumentIds(string dataSetName, string query, IEnumerable<string> tagIds, DateTime? dateStart = null, DateTime? dateEnd = null)
        {
            var globalStoreDataSet = GlobalStore.DataSets.Get(dataSetName);
            var dataSet = globalStoreDataSet.DataSet;
            var documentQuery = queryFactory.GetDocumentQuery(dataSetName);
            var documentElasticIds = new List<string>();

            var scrollResult = documentQuery
                .Filter(
                    query,
                    tagIds,
                    dataSet.TagField,
                    -1, null, false,
                    dataSet.InterpretedFields,
                    globalStoreDataSet.DocumentFields,
                    DocumentService.GetFieldFilter(globalStoreDataSet, new List<string> { dataSet.IdField }),
                    null,
                    dateStart,
                    dateEnd);

            documentElasticIds.AddRange(scrollResult.Items.Select(i => i.Id));

            while (scrollResult.Items.Any())
            {
                scrollResult = documentQuery.GetScrolled(scrollResult.ScrollId);
                documentElasticIds.AddRange(scrollResult.Items.Select(i => i.Id));
            }

            return documentElasticIds;
        }

        private Scorer GetScorer(Subset globalSubset, Dictionary<string, Occurences> wwoBase, string baseCleanedText, PeSScorer globalScorer)
        {
            var wordsInDic = globalSubset.WordsWithOccurences.Keys.Intersect(wwoBase.Keys).ToList();

            var baseSubset = new Subset
            {
                AllWordsOccurencesSumInCorpus = globalSubset.AllWordsOccurencesSumInCorpus,
                AllWordsOccurencesSumInTag = globalSubset.AllWordsOccurencesSumInTag,
                WordsWithOccurences = wordsInDic.ToDictionary(w => w, w => globalSubset.WordsWithOccurences[w])
            };
            var baseDic = new TwisterAlgorithm(baseSubset, true, false).GetDictionary();

            // kiszámoljuk a base_score-t és a global_score-t
            var baseScorer = new PeSScorer(new Dictionary<int, Dictionary<string, double>> { { 1, baseDic } });

            var baseScore = baseScorer.GetScore(baseCleanedText, ScoreMultiplier);
            if (baseScore == 0)
            {
                return null;
            }

            var globalScore = globalScorer.GetScore(baseCleanedText, ScoreMultiplier);
            if (globalScore == 0)
            {
                return null;
            }

            return new Scorer()
            {
                BaseDic = baseDic,
                BaseScorer = baseScorer,
                BaseScore = baseScore,
                GlobalScorer = globalScorer,
                GlobalScore = globalScore
            };
        }

        private static string GetCleanedText(Dictionary<string, Occurences> wwo)
        {
            return string.Join(" ", wwo.Select(w => string.Join(" ", Enumerable.Repeat(w.Key, w.Value.Tag))));
        }

        private static double GetPrcScore(Scorer scorer, string cleanedText)
        {
            var baseScore = scorer.BaseScorer.GetScore(cleanedText, ScoreMultiplier);
            if (baseScore == 0)
            {
                return 0;
            }

            var globalScore = scorer.GlobalScorer.GetScore(cleanedText, ScoreMultiplier);
            if (globalScore == 0)
            {
                return 0;
            }

            var finalScore = (baseScore / scorer.BaseScore) / (globalScore / scorer.GlobalScore);

            return finalScore;
        }

        internal class Scorer
        {
            public Dictionary<string, double> BaseDic { get; set; }
            public PeSScorer BaseScorer { get; set; }
            public PeSScorer GlobalScorer { get; set; }
            public double BaseScore { get; set; }
            public double GlobalScore { get; set; }
        }
    }
}