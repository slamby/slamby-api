using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Services;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.Common.Helpers;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using Swashbuckle.SwaggerGen.Annotations;
using Slamby.Common.Services;
using System.Collections.Concurrent;
using Slamby.API.Services.Interfaces;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.API.Filters;

namespace Slamby.API.Controllers.Services
{
    [Route("api/Services/Prc")]
    [SwaggerGroup("PrcService")]
    [SwaggerResponseRemoveDefaults]
    public class PrcController : BaseController
    {
        readonly ServiceQuery serviceQuery;
        readonly PrcServiceHandler prcHandler;
        readonly IQueryFactory queryFactory;
        readonly ProcessHandler processHandler;
        readonly ParallelService parallelService;
        readonly ServiceManager serviceManager;

        public IGlobalStoreManager GlobalStore { get; set; }

        readonly PrcIndexServiceHandler prcIndexHandler;

        public PrcController(ServiceQuery serviceQuery, PrcServiceHandler prcHandler, IQueryFactory queryFactory, ProcessHandler processHandler,
            ParallelService parallelService, ServiceManager serviceManager, IGlobalStoreManager globalStore, PrcIndexServiceHandler prcIndexHandler)
        {
            this.prcIndexHandler = prcIndexHandler;
            GlobalStore = globalStore;
            this.serviceManager = serviceManager;
            this.parallelService = parallelService;
            this.processHandler = processHandler;
            this.queryFactory = queryFactory;
            this.prcHandler = prcHandler;
            this.serviceQuery = serviceQuery;
        }

        [HttpGet("{id}")]
        [SwaggerOperation("PrcGetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(PrcService))]
        public IActionResult Get(string id)
        {
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }

            PrcActivateSettings activateSettings = null;
            PrcPrepareSettings prepareSettings = null;
            PrcIndexSettings indexSettings = null;

            var prcSettingsElastic = serviceQuery.GetSettings<PrcSettingsElastic>(service.Id);
            if (prcSettingsElastic != null)
            {
                if (service.Status == (int)ServiceStatusEnum.Prepared || service.Status == (int)ServiceStatusEnum.Active)
                {
                    prepareSettings = new PrcPrepareSettings
                    {
                        DataSetName = GlobalStore.DataSets.Get(prcSettingsElastic.DataSetName).AliasName,
                        TagIdList = prcSettingsElastic.Tags.Select(t => t.Id).ToList(),
                        CompressSettings = CompressHelper.ToCompressSettings(prcSettingsElastic.CompressSettings),
                        CompressLevel = CompressHelper.ToCompressLevel(prcSettingsElastic.CompressSettings)
                    };
                    if (service.Status == (int)ServiceStatusEnum.Active)
                    {
                        activateSettings = new PrcActivateSettings
                        {
                            FieldsForRecommendation = prcSettingsElastic.FieldsForRecommendation,
                            DestinationDataSetName = GlobalStore.DataSets.Get(prcSettingsElastic.DestinationDataSetName).AliasName,
                        };

                        if (prcSettingsElastic?.IndexSettings?.IndexDate != null)
                        {
                            indexSettings = new PrcIndexSettings()
                            {
                                Filter = new Filter()
                                {
                                    Query = prcSettingsElastic.IndexSettings.FilterQuery,
                                    TagIdList = prcSettingsElastic.IndexSettings.FilterTagIdList
                                }
                            };
                        }
                    }
                }
            }

            var respService = service.ToServiceModel<PrcService>();
            respService.ActualProcessId = service.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
            respService.ActivateSettings = activateSettings;
            respService.PrepareSettings = prepareSettings;
            respService.IndexSettings = indexSettings;

            return new OkObjectResult(respService);
        }

        [HttpPost("{id}/Prepare")]
        [SwaggerOperation("PrcPrepareService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult Prepare(string id, [FromBody]PrcPrepareSettings prcPrepareSettings)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.New)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithNewStatusCanBePrepared);
            }

            //DATASET VALIDATION
            if (!GlobalStore.DataSets.IsExist(prcPrepareSettings.DataSetName))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.DataSet_0_NotFound, prcPrepareSettings.DataSetName));
            }

            var globalStoreDataSet = GlobalStore.DataSets.Get(prcPrepareSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;

            //TAGS VALIDATION
            var tagQuery = queryFactory.GetTagQuery(dataSet.Name);
            List<TagElastic> tags;
            if (prcPrepareSettings?.TagIdList?.Any() == true)
            {
                tags = tagQuery.Get(prcPrepareSettings.TagIdList).ToList();
                if (tags.Count < prcPrepareSettings.TagIdList.Count)
                {
                    var missingTagIds = prcPrepareSettings.TagIdList.Except(tags.Select(t => t.Id)).ToList();
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheDataSet_0, string.Join(", ", missingTagIds)));
                }
            }
            else
            {
                tags = tagQuery.GetAll().Items.Where(i => i.IsLeaf).ToList();
            }

            //SAVE SETTINGS TO ELASTIC
            var serviceSettings = new PrcSettingsElastic
            {
                DataSetName = globalStoreDataSet.IndexName,
                ServiceId = service.Id,
                Tags = tags,
                CompressSettings = CompressHelper.ToCompressSettingsElastic(prcPrepareSettings.CompressSettings, prcPrepareSettings.CompressLevel)
            };
            serviceQuery.IndexSettings(serviceSettings);

            var process = processHandler.Create(
                ProcessTypeEnum.PrcPrepare,
                service.Id,
                prcPrepareSettings,
                string.Format(ServiceResources.Preparing_0_Service_1, ServiceTypeEnum.Prc, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => prcHandler.Prepare(process.Id, serviceSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Activate")]
        [SwaggerOperation("PrcActivateService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult Activate(string id, [FromBody]PrcActivateSettings prcActivateSettings)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.Prepared)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithPreparedStatusCanBeActivated);
            }

            var prcSettings = serviceQuery.GetSettings<PrcSettingsElastic>(service.Id);

            if (prcActivateSettings == null)
            {
                if (prcSettings.FieldsForRecommendation == null)
                {
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.EmptyActivateSettings);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(prcActivateSettings.DestinationDataSetName))
                {
                    //DATASET VALIDATION
                    if (!GlobalStore.DataSets.IsExist(prcActivateSettings.DestinationDataSetName))
                    {
                        return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.DataSet_0_NotFound, prcActivateSettings.DestinationDataSetName));
                    }
                    var globalStoreDataSet = GlobalStore.DataSets.Get(prcActivateSettings.DestinationDataSetName);
                    prcSettings.DestinationDataSetName = globalStoreDataSet.IndexName;
                }
                else
                {
                    prcSettings.DestinationDataSetName = prcSettings.DataSetName;
                }
                if (prcActivateSettings.FieldsForRecommendation?.Any() == true)
                {
                    var fields = prcActivateSettings.FieldsForRecommendation.Intersect(GlobalStore.DataSets.Get(prcSettings.DestinationDataSetName).DataSet.InterpretedFields).ToList();
                    if (fields.Count != prcActivateSettings.FieldsForRecommendation.Count)
                    {
                        return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                        string.Format(ServiceResources.TheFollowingFieldsNotExistInTheSampleDocument_0, string.Join(", ",
                        prcActivateSettings.FieldsForRecommendation.Except(GlobalStore.DataSets.Get(prcSettings.DestinationDataSetName).DataSet.InterpretedFields).ToList())));
                    }
                    prcSettings.FieldsForRecommendation = prcActivateSettings.FieldsForRecommendation;
                }
                else
                {
                    prcSettings.FieldsForRecommendation = GlobalStore.DataSets.Get(prcSettings.DestinationDataSetName).DataSet.InterpretedFields;
                }
            }

            var process = processHandler.Create(
                ProcessTypeEnum.PrcActivate,
                service.Id,
                prcSettings,
                string.Format(ServiceResources.Activating_0_Service_1, ServiceTypeEnum.Prc, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);
            serviceQuery.IndexSettings(prcSettings);

            processHandler.Start(process, (tokenSource) => prcHandler.Activate(process.Id, prcSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Deactivate")]
        [SwaggerOperation("PrcDeactivateService")]
        [SwaggerResponse(StatusCodes.Status200OK, "")]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult Deactivate(string id)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.Active)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithActiveStatusCanBeDeactivated);
            }

            prcHandler.Deactivate(service.Id);

            service.Status = (int)ServiceStatusEnum.Prepared;
            serviceQuery.Update(service.Id, service);

            return new OkResult();
        }

        [HttpPost("{id}/Keywords")]
        [SwaggerOperation("PrcKeywordsService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<PrcKeywordsResult>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable)]
        public IActionResult Keywords(string id, [FromBody]PrcKeywordsRequest request, [FromQuery]bool isStrict = false)
        {
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(id))
            {
                id = GlobalStore.ServiceAliases.Get(id);
            }

            if (!GlobalStore.ActivatedPrcs.IsExist(id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.ServiceNotExistsOrNotActivated, ServiceTypeEnum.Prc));
            }
            if (!string.IsNullOrEmpty(request.TagId) && !GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.Tags.Any(t => t.Id == request.TagId))
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.TheGivenTagIsMissingFromThePRCService);

            var dataSet = GlobalStore.DataSets.Get(GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.DataSetName).DataSet;
            var analyzeQuery = queryFactory.GetAnalyzeQuery(dataSet.Name);

            var tokens = analyzeQuery.Analyze(request.Text, 1).ToList();
            var text = string.Join(" ", tokens);

            var tagId = string.Empty;
            if (!string.IsNullOrEmpty(request.TagId))
            {
                tagId = request.TagId;
            }
            else
            {
                //ha nincs megadva tagId akkor kiszámoljuk a prc scorer-ekkel
                var allResults = new List<KeyValuePair<string, double>>();
                foreach (var scorerKvp in GlobalStore.ActivatedPrcs.Get(id).PrcScorers)
                {
                    var score = scorerKvp.Value.GetScore(text, 1.7, true);
                    allResults.Add(new KeyValuePair<string, double>(scorerKvp.Key, score));
                }
                var resultsList = allResults.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ToList();
                if (resultsList.Count == 0) return new OkObjectResult(new List<PrcRecommendationResult>());
                tagId = resultsList.First().Key;
            }

            var globalSubset = GlobalStore.ActivatedPrcs.Get(id).PrcSubsets[tagId];
            if (globalSubset.WordsWithOccurences == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status406NotAcceptable, ServiceResources.TheGivenTagHasNoWordsInDictionary);
            }
            var wordsInDic = globalSubset.WordsWithOccurences.Keys.Intersect(tokens).ToList();

            var baseSubset = new Cerebellum.Subset
            {
                AllWordsOccurencesSumInCorpus = globalSubset.AllWordsOccurencesSumInCorpus,
                AllWordsOccurencesSumInTag = globalSubset.AllWordsOccurencesSumInTag,
                WordsWithOccurences = wordsInDic.ToDictionary(w => w, w => globalSubset.WordsWithOccurences[w])
            };
            var baseDic = new Cerebellum.Dictionary.TwisterAlgorithm(baseSubset, true, false).GetDictionary().OrderByDescending(d => d.Value).ToList();
            if (isStrict)
            {
                var avg = baseDic.Sum(d => d.Value) / baseDic.Count;
                baseDic.RemoveAll(d => d.Value < avg);
            }
            return new OkObjectResult(baseDic.Select(d => new PrcKeywordsResult { Word = d.Key, Score = d.Value }));
        }

        [HttpPost("{id}/Recommend")]
        [SwaggerOperation("PrcRecommendService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<PrcRecommendationResult>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable)]
        public IActionResult Recommend(string id, [FromBody]PrcRecommendationRequest request, bool isStrict = false)
        {
            if (request == null) return new StatusCodeResult(StatusCodes.Status400BadRequest);
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(id))
            {
                id = GlobalStore.ServiceAliases.Get(id);
            }

            if (!GlobalStore.ActivatedPrcs.IsExist(id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.ServiceNotExistsOrNotActivated, ServiceTypeEnum.Prc));
            }

            if (!string.IsNullOrEmpty(request.TagId) && !GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.Tags.Any(t => t.Id == request.TagId))
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.TheGivenTagIsMissingFromThePRCService);


            var globalStoreDataSet = GlobalStore.DataSets.Get(GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;
            var analyzeQuery = queryFactory.GetAnalyzeQuery(dataSet.Name);

            var tokens = analyzeQuery.Analyze(request.Text, 1).ToList();
            var text = string.Join(" ", tokens);

            //tagId meghatározása
            var tagId = string.Empty;
            if (!string.IsNullOrEmpty(request.TagId))
            {
                tagId = request.TagId;
            }
            else
            {
                //ha nincs megadva tagId akkor kiszámoljuk a prc scorer-ekkel
                var allResults = new List<KeyValuePair<string, double>>();
                foreach (var scorerKvp in GlobalStore.ActivatedPrcs.Get(id).PrcScorers)
                {
                    var score = scorerKvp.Value.GetScore(text, 1.7, true);
                    allResults.Add(new KeyValuePair<string, double>(scorerKvp.Key, score));
                }
                var resultsList = allResults.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ToList();
                if (resultsList.Count == 0) return new OkObjectResult(new List<PrcRecommendationResult>());
                tagId = resultsList.First().Key;
            }

            var globalStoreDestinationDataSet = GlobalStore.DataSets.Get(GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.DestinationDataSetName);
            var destinationDataSet = globalStoreDestinationDataSet.DataSet;

            var tagsToTest = new List<string>();
            if (request.Filter?.TagIdList?.Any() == true)
            {
                /*var existingTags = GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.Tags.Select(t => t.Id).Intersect(request.Filter.TagIdList).ToList();
                if (existingTags.Count < request.Filter.TagIdList.Count)
                {
                    var missingTagIds = request.Filter.TagIdList.Except(existingTags).ToList();
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheDataSet_0, string.Join(", ", missingTagIds)));
                }*/
                // TODO validate with the destination dataset tags
                tagsToTest = request.Filter.TagIdList;
            }

            var globalSubset = GlobalStore.ActivatedPrcs.Get(id).PrcSubsets[tagId];
            if (globalSubset.WordsWithOccurences == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status406NotAcceptable, ServiceResources.TheGivenTagHasNoWordsInDictionary);
            }

            var wordsInDic = globalSubset.WordsWithOccurences.Keys.Intersect(tokens).ToList();

            var baseSubset = new Cerebellum.Subset
            {
                AllWordsOccurencesSumInCorpus = globalSubset.AllWordsOccurencesSumInCorpus,
                AllWordsOccurencesSumInTag = globalSubset.AllWordsOccurencesSumInTag,
                WordsWithOccurences = wordsInDic.ToDictionary(w => w, w => globalSubset.WordsWithOccurences[w])
            };
            var baseDic = new Cerebellum.Dictionary.TwisterAlgorithm(baseSubset, true, false).GetDictionary();

            if (isStrict)
            {
                var avg = baseDic.Sum(d => d.Value) / baseDic.Count;
                baseDic.Where(d => d.Value < avg).ToList().ForEach(d => baseDic.Remove(d.Key));
            }

            var globalScorer = GlobalStore.ActivatedPrcs.Get(id).PrcScorers[tagId];
            var baseScorer = new Cerebellum.Scorer.PeSScorer(new Dictionary<int, Dictionary<string, double>> { { 1, baseDic } });

            var baseScore = baseScorer.GetScore(text, 1.7);
            var globalScore = globalScorer.GetScore(text, 1.7);

            var results = new List<PrcRecommendationResult>();

            if (baseScore == 0 || globalScore == 0)
            {
                return new OkObjectResult(results);
            }

            var filterQuery = request.Filter?.Query?.Trim();
            var query = string.IsNullOrEmpty(filterQuery) ? string.Empty : $"({filterQuery}) AND ";
            // '+ 1' because we give score between 0 and 1 but in elasticsearch that means negative boost
            query = string.Format("{0}({1})", query, string.Join(" ", baseDic.Select(k => $"{k.Key}^{k.Value + 1}")));

            string shouldQuery = null;
            // weighting
            if (request.Weights?.Any() == true)
            {
                shouldQuery = string.Join(" ", request.Weights.Select(k => $"({k.Query})^{k.Value}"));
            }

            var fieldsForRecommendation = GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.FieldsForRecommendation;

            Func<string, bool> isAttachmentField = (field) => globalStoreDestinationDataSet.AttachmentFields.Any(attachmentField =>
                string.Equals(attachmentField, field, StringComparison.OrdinalIgnoreCase));

            var fieldList = fieldsForRecommendation
                .Select(field => isAttachmentField(field) ? $"{field}.content" : field)
                .Select(DocumentQuery.MapDocumentObjectName)
                .ToList();

            var documentQuery = queryFactory.GetDocumentQuery(destinationDataSet.Name);
            var documentElastics = new List<DocumentElastic>();
            var scrollResult = documentQuery
                .Filter(query,
                        tagsToTest,
                        destinationDataSet.TagField,
                        request.Count,
                        null, false,
                        fieldsForRecommendation,
                        globalStoreDestinationDataSet.DocumentFields,
                        DocumentService.GetFieldFilter(globalStoreDestinationDataSet, new List<string> { request.NeedDocumentInResult ? "*" : globalStoreDestinationDataSet.DataSet.IdField }),
                        null, null, null,
                        shouldQuery);

            documentElastics.AddRange(scrollResult.Items);

            var docIdsWithScore = new ConcurrentDictionary<string, double>(new Dictionary<string, double>());
            var wordQuery = queryFactory.GetWordQuery(destinationDataSet.Name);
            
            Parallel.ForEach(documentElastics, parallelService.ParallelOptions(), docElastic =>
            {
                var wwo = wordQuery.GetWordsWithOccurences(new List<string> { docElastic.Id }, fieldList, 1);
                var actualCleanedText = string.Join(" ", wwo.Select(w => string.Join(" ", Enumerable.Repeat(w.Key, w.Value.Tag))));

                var actualBaseScore = baseScorer.GetScore(actualCleanedText, 1.7);
                if (actualBaseScore == 0) return;

                var actualGlobalScore = globalScorer.GetScore(actualCleanedText, 1.7);
                if (actualGlobalScore == 0) return;

                var finalScore = (actualBaseScore / baseScore) / (actualGlobalScore / globalScore);
                docIdsWithScore.TryAdd(docElastic.Id, finalScore);
            });

            var resultDic = docIdsWithScore.OrderByDescending(rd => rd.Value).ToList();
            if (request.Count != 0 && resultDic.Count > request.Count) resultDic = resultDic.Take(request.Count).ToList();

            var docsDic = request.NeedDocumentInResult
                ? resultDic.Select(r => documentElastics.First(d => d.Id == r.Key)).ToDictionary(d => d.Id, d => d)
                : null;

            return new OkObjectResult(resultDic.Select(kvp => new PrcRecommendationResult
            {
                DocumentId = kvp.Key,
                Score = kvp.Value,
                Document = request.NeedDocumentInResult ? docsDic[kvp.Key].DocumentObject : null
            }));
        }

        [HttpPost("{id}/Index")]
        [SwaggerOperation("PrcIndexService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult Index(string id, [FromBody]PrcIndexSettings prcIndexSettings)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.Active)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    ServiceResources.InvalidStatusOnlyTheServicesWithActiveStatusCanBeIndexed);
            }

            var prcSettings = serviceQuery.GetSettings<PrcSettingsElastic>(service.Id);

            var preparedTagIds = prcSettings.Tags.Select(t => t.Id).ToList();
            var indexFilterTagIds = prcIndexSettings?.Filter?.TagIdList?.Any() == true
                ? prcIndexSettings.Filter.TagIdList
                : preparedTagIds;

            var tagIds = preparedTagIds.Intersect(indexFilterTagIds).ToList();
            if (tagIds.Count < indexFilterTagIds.Count)
            {
                var missingTagIds = indexFilterTagIds.Except(preparedTagIds).ToList();
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    string.Format(ServiceResources.TheFollowingTagIdsWereNotPrepared_0, string.Join(", ", missingTagIds)));
            }

            prcSettings.IndexSettings = new IndexSettingsElastic
            {
                IndexDate = DateTime.UtcNow,
                FilterQuery = prcIndexSettings?.Filter?.Query,
                FilterTagIdList = indexFilterTagIds
            };

            serviceQuery.IndexSettings(prcSettings);

            var process = processHandler.Create(
                ProcessTypeEnum.PrcIndex,
                service.Id,
                prcSettings,
                string.Format(ServiceResources.Indexing_0_Service_1, ServiceTypeEnum.Prc, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => prcIndexHandler.Index(process.Id, prcSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/IndexPartial")]
        [SwaggerOperation("PrcIndexPartialService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult IndexPartial(string id)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.Active)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    ServiceResources.InvalidStatusOnlyTheServicesWithActiveStatusCanBeIndexed);
            }

            var prcSettings = serviceQuery.GetSettings<PrcSettingsElastic>(service.Id);

            if (prcSettings?.IndexSettings?.IndexDate == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    ServiceResources.IndexPartialCanBeCalledAfterCallingIndex);
            }

            var process = processHandler.Create(
                ProcessTypeEnum.PrcIndexPartial,
                service.Id,
                prcSettings,
                string.Format(ServiceResources.PartialIndexing_0_Service_1, ServiceTypeEnum.Prc, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => prcIndexHandler.IndexPartial(process.Id, prcSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/RecommendById")]
        [SwaggerOperation("PrcRecommendByIdService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<PrcRecommendationResult>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult RecommendById(string id, [FromBody]PrcRecommendationByIdRequest request)
        {
            if (request == null) return new StatusCodeResult(StatusCodes.Status400BadRequest);
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(id))
            {
                id = GlobalStore.ServiceAliases.Get(id);
            }

            if (!GlobalStore.ActivatedPrcs.IsExist(id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.ServiceNotExistsOrNotActivated, ServiceTypeEnum.Prc));
            }
            if (!string.IsNullOrEmpty(request.TagId) && !GlobalStore.ActivatedPrcs.Get(id).PrcsSettings.Tags.Any(t => t.Id == request.TagId))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.TheGivenTagIsMissingFromThePRCService);
            }

            var prcSettings = GlobalStore.ActivatedPrcs.Get(id).PrcsSettings;
            var result = prcIndexHandler.RecommendById(id, prcSettings, request);

            return new OkObjectResult(result);
        }

        [HttpPost("{id}/ExportDictionaries")]
        [SwaggerOperation("PrcExportDictionaries")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        [ServiceFilter(typeof(ServiceBusyFilter))]
        public IActionResult ExportDictionaries(string id, [FromBody]ExportDictionariesSettings settings, [FromServices]UrlProvider urlProvider)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Prc)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Prc"));
            }
            if (service.Status != (int)ServiceStatusEnum.Active && service.Status != (int)ServiceStatusEnum.Prepared)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithPreparedOrActiveStatusCanBeExported);
            }

            var prcSettings = serviceQuery.GetSettings<PrcSettingsElastic>(service.Id);

            //TAGS VALIDATION
            var tagQuery = queryFactory.GetTagQuery(prcSettings.DataSetName);
            List<string> tagIds;
            if (settings?.TagIdList?.Any() == true)
            {
                tagIds = prcSettings.Tags.Select(t => t.Id).Intersect(settings.TagIdList).ToList();
                if (tagIds.Count < settings.TagIdList.Count)
                {
                    var missingTagIds = settings.TagIdList.Except(tagIds).ToList();
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheService_0, string.Join(", ", missingTagIds)));
                }
            }
            else
            {
                tagIds = prcSettings.Tags.Select(t => t.Id).ToList();
            }

            var process = processHandler.Create(
                ProcessTypeEnum.PrcExportDictionaries,
                service.Id, settings,
                string.Format(ServiceResources.ExportingDictionariesFrom_0_Service_1, ServiceTypeEnum.Prc, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => prcHandler.ExportDictionaries(process.Id, prcSettings, tagIds, tokenSource.Token, urlProvider.GetHostUrl()));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }
    }
}