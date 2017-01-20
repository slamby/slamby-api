using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.Elastic.Queries;
using Slamby.API.Helpers;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.API.Services.Interfaces;
using Swashbuckle.SwaggerGen.Annotations;
using Microsoft.AspNetCore.Http;
using Slamby.SDK.Net.Models.Services;
using Slamby.SDK.Net.Models.Enums;
using Slamby.Common.Helpers;
using Slamby.API.Resources;
using Slamby.SDK.Net.Models;
using Slamby.Elastic.Models;
using Slamby.API.Filters;
using Slamby.API.Helpers.Services;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Slamby.API.Controllers.Services
{
    [Route("api/Services/Search")]
    [SwaggerGroup("SearchService")]
    public class SearchController : BaseController
    {
        readonly ServiceQuery serviceQuery;
        readonly SearchServiceHandler searchHandler;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;

        public IGlobalStoreManager GlobalStore { get; set; }

        public SearchController(ServiceQuery serviceQuery, SearchServiceHandler searchHandler, ProcessHandler processHandler,
            IQueryFactory queryFactory, IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.searchHandler = searchHandler;
            this.serviceQuery = serviceQuery;
        }

        [HttpGet("{id}")]
        [SwaggerOperation("SearchGetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(SearchSettingsWrapperElastic))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Get(string id)
        {
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }
            if (service.Type != (int)ServiceTypeEnum.Search)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Search"));
            }

            SearchActivateSettings activateSettings = null;
            SearchPrepareSettings prepareSettings = null;

            var searchSettingsElastic = serviceQuery.GetSettings<SearchSettingsWrapperElastic>(service.Id);
            if (searchSettingsElastic != null)
            {
                prepareSettings = searchSettingsElastic.ToSearchPrepareSettingsModel();
                activateSettings = searchSettingsElastic.ToSearchActivateSettingsModel();
            }

            var respService = service.ToServiceModel<SearchService>();
            respService.ActualProcessId = service.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
            respService.ActivateSettings = activateSettings;
            respService.PrepareSettings = prepareSettings;

            return new OkObjectResult(respService);
        }

        [HttpPost("{id}/Prepare")]
        [SwaggerOperation("SearchPrepareService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        public IActionResult Prepare(string id, [FromBody]SearchPrepareSettings searchPrepareSettings)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Search)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Search"));
            }
            if (service.Status != (int)ServiceStatusEnum.New)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithNewStatusCanBePrepared);
            }

            //DATASET VALIDATION
            if (!GlobalStore.DataSets.IsExist(searchPrepareSettings.DataSetName))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                    string.Format(ServiceResources.DataSet_0_NotFound, searchPrepareSettings.DataSetName));
            }

            // TODO VALIDATION

            var globalStoreDataSet = GlobalStore.DataSets.Get(searchPrepareSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;



            var serviceSettings = new SearchSettingsWrapperElastic
            {
                DataSetName = globalStoreDataSet.DataSet.Name,
                ServiceId = service.Id
            };

            // SETUP default values for Activation - later here we can calculate accurate settings for the dataset

            var defaultActivationSettings = new SearchActivateSettings();
            serviceSettings.Count = defaultActivationSettings.Count;
            serviceSettings.HighlightSettings = null;

            var defaultAutoCompleteSettings = new AutoCompleteSettings();
            serviceSettings.AutoCompleteSettings = new AutoCompleteSettingsElastic
            {
                Confidence = defaultAutoCompleteSettings.Confidence,
                Count = defaultActivationSettings.Count,
                HighlightSettings = null,
                MaximumErrors = defaultAutoCompleteSettings.MaximumErrors,
                NGram = dataSet.NGramCount
            };

            var defaultSearchSettings = new SearchSettings();
            serviceSettings.SearchSettings = new SearchSettingsElastic
            {
                Count = defaultActivationSettings.Count,
                CutOffFrequency = defaultSearchSettings.CutOffFrequency,
                Filter = null,
                Fuzziness = defaultSearchSettings.Fuzziness,
                HighlightSettings = null,
                ResponseFieldList = dataSet.InterpretedFields.Union(new List<string> { dataSet.IdField, dataSet.TagField }).ToList(),
                SearchFieldList = dataSet.InterpretedFields,
                Type = (int)SearchTypeEnum.Match,
                Weights = null,
                Operator = (int)defaultSearchSettings.Operator
            };

            serviceQuery.IndexSettings(serviceSettings);

            var process = processHandler.Create(
                ProcessTypeEnum.SearchPrepare,
                service.Id, searchPrepareSettings,
                string.Format(ServiceResources.Preparing_0_Service_1, ServiceTypeEnum.Search, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => searchHandler.Prepare(process.Id, serviceSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Activate")]
        [SwaggerOperation("SearchActivateService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Activate(string id, [FromBody]SearchActivateSettings searchActivateSettings)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Search)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Search"));
            }
            if (service.Status != (int)ServiceStatusEnum.Prepared)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithPreparedStatusCanBeActivated);
            }
            var searchSettings = serviceQuery.GetSettings<SearchSettingsWrapperElastic>(service.Id);
            service.Status = (int)ServiceStatusEnum.Active;

            if (searchActivateSettings != null)
            {
                // TODO VALIDATION

                searchSettings = MergeSettings(
                    searchSettings,
                    searchActivateSettings.AutoCompleteSettings,
                    searchActivateSettings.ClassifierSettings,
                    searchActivateSettings.SearchSettings,
                    searchActivateSettings.HighlightSettings,
                    searchActivateSettings.Count);
            }

            var process = processHandler.Create(
                ProcessTypeEnum.ClassifierActivate,
                service.Id,
                searchSettings,
                string.Format(ServiceResources.Activating_0_Service_1, ServiceTypeEnum.Search, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceQuery.Update(service.Id, service);
            serviceQuery.IndexSettings(searchSettings);

            processHandler.Start(process, (tokenSource) => searchHandler.Activate(process.Id, searchSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Deactivate")]
        [SwaggerOperation("SearchDeactivateService")]
        [SwaggerResponse(StatusCodes.Status200OK, "")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Deactivate(string id)
        {
            //SERVICE VALIDATION
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Search)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Search"));
            }
            if (service.Status != (int)ServiceStatusEnum.Active)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithActiveStatusCanBeDeactivated);
            }

            searchHandler.Deactivate(service.Id);

            service.Status = (int)ServiceStatusEnum.Prepared;
            serviceQuery.Update(service.Id, service);

            return new OkResult();
        }

        [HttpPost("{id}")]
        [SwaggerOperation("SearchService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<SearchResultWrapper>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Search(string id, [FromBody]SearchRequest request)
        {
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(id))
            {
                id = GlobalStore.ServiceAliases.Get(id);
            }

            if (!GlobalStore.ActivatedSearches.IsExist(id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.ServiceNotExistsOrNotActivated);
            }

            // TODO VALIDATION

            var searchSettings = MergeSettings(
                GlobalStore.ActivatedSearches.Get(id).SearchSettingsWrapper,
                request.AutoCompleteSettings,
                request.ClassifierSettings,
                request.SearchSettings,
                request.HighlightSettings,
                request.Count);
            
            var dataSet = GlobalStore.DataSets.Get(searchSettings.DataSetName);
            var result = new SearchResultWrapper();

            var documentQuery = queryFactory.GetDocumentQuery(dataSet.DataSet.Name);
            var searchResponse = documentQuery.Search(
                searchSettings.AutoCompleteSettings,
                searchSettings.SearchSettings,
                request.Text,
                dataSet.DocumentFields,
                dataSet.DataSet.TagField
            );

            // AUTOCOMPLETE
            // TODO collate script + highlight!
            result.AutoCompleteResultList = searchResponse.Suggest?["simple_suggest"].SelectMany(s => s.Options).Select(o =>
                new AutoCompleteResult
                {
                    Text = searchSettings.AutoCompleteSettings.HighlightSettings == null ? o.Text : o.Highlighted,
                    Score = o.Score,
                }).ToList();

            // SEARCH
            // TODO highlight
            result.SearchResultList = searchResponse.Hits.Select(d =>
                new SearchResult
                {
                    Document = d.Source.DocumentObject,
                    DocumentId = d.Id,
                    Score = d.Score
                }).ToList();


            // CLASSIFIER
            if (searchSettings.ClassifierSettings != null)
            {
                var analyzeQuery = queryFactory.GetAnalyzeQuery(dataSet.DataSet.Name);
                var classifierId = searchSettings.ClassifierSettings.Id;
                var classifier = GlobalStore.ActivatedClassifiers.Get(GlobalStore.ServiceAliases.IsExist(classifierId) ? GlobalStore.ServiceAliases.Get(classifierId) : classifierId);
                //a bi/tri stb gramokat nem jobb lenne elastic-al? Jelenleg a Scorer csinálja az NGramMaker-el

                //ORIGINAL
                var tokens = analyzeQuery.Analyze(request.Text, 1).ToList();
                var text = string.Join(" ", tokens);

                var allResults = new List<KeyValuePair<string, double>>();
                foreach (var scorerKvp in classifier.ClassifierScorers)
                {
                    var score = scorerKvp.Value.GetScore(text, 1.7, true);
                    allResults.Add(new KeyValuePair<string, double>(scorerKvp.Key, score));
                }
                var resultsList = allResults.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ToList();
                if (resultsList.Count > searchSettings.ClassifierSettings.Count) resultsList = resultsList.Take(searchSettings.ClassifierSettings.Count).ToList();
                result.ClassifierResultList = resultsList.Select(r => new ClassifierRecommendationResult
                {
                    TagId = r.Key,
                    Score = r.Value,
                    Tag = classifier.ClassifiersTags[r.Key]
                }).ToList();


                //AUTOCOMPLETE
                foreach (var ac in result.AutoCompleteResultList)
                {
                    tokens = analyzeQuery.Analyze(ac.Text, 1).ToList();
                    text = string.Join(" ", tokens);

                    allResults = new List<KeyValuePair<string, double>>();
                    foreach (var scorerKvp in classifier.ClassifierScorers)
                    {
                        var score = scorerKvp.Value.GetScore(text, 1.7, true);
                        allResults.Add(new KeyValuePair<string, double>(scorerKvp.Key, score));
                    }
                    resultsList = allResults.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ToList();
                    if (searchSettings.ClassifierSettings.Count != 0 &&
                        resultsList.Count > searchSettings.ClassifierSettings.Count) resultsList = resultsList.Take(searchSettings.ClassifierSettings.Count).ToList();
                    ac.ClassifierResultList = resultsList.Select(r => new ClassifierRecommendationResult
                    {
                        TagId = r.Key,
                        Score = r.Value,
                        Tag = classifier.ClassifiersTags[r.Key]
                    }).ToList();
                }
            }
            return new OkObjectResult(result);
        }

        private SearchSettingsWrapperElastic MergeSettings(
            SearchSettingsWrapperElastic defaultSettings,
            AutoCompleteSettings autoCompleteSettings, ClassifierSettings classifierSettings, SearchSettings searchSettings, HighlightSettings highlightSettings, int count)
        {
            if (autoCompleteSettings != null ||
                classifierSettings != null ||
                searchSettings != null)
            {
                defaultSettings.AutoCompleteSettings = autoCompleteSettings?.ToAutoCompleteSettingsElastic();
                defaultSettings.ClassifierSettings = classifierSettings?.ToClassifierSearchSettingsElastic();
                defaultSettings.SearchSettings = searchSettings?.ToSearchSettingsElastic();
                defaultSettings.HighlightSettings = highlightSettings?.ToHighlightSettingsElastic();
            }

            //set the default count from the root
            if (count > 0)
            {
                if (defaultSettings.AutoCompleteSettings?.Count == 0) autoCompleteSettings.Count = count;
                if (defaultSettings.SearchSettings?.Count == 0) defaultSettings.SearchSettings.Count = count;
                if (defaultSettings.ClassifierSettings?.Count == 0) defaultSettings.ClassifierSettings.Count = count;
            }

            //set the default highlight from the root
            if (highlightSettings != null)
            {
                if (defaultSettings.AutoCompleteSettings?.HighlightSettings == null)
                    defaultSettings.AutoCompleteSettings.HighlightSettings = highlightSettings.ToHighlightSettingsElastic();
                if (defaultSettings.SearchSettings?.HighlightSettings == null)
                    defaultSettings.SearchSettings.HighlightSettings = highlightSettings.ToHighlightSettingsElastic();
            }

            return defaultSettings;
        }
    }
}
