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
using Slamby.API.Services;

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
        readonly ServiceManager serviceManager;

        public IGlobalStoreManager GlobalStore { get; set; }

        public SearchController(ServiceQuery serviceQuery, SearchServiceHandler searchHandler, ProcessHandler processHandler,
            IQueryFactory queryFactory, IGlobalStoreManager globalStore, ServiceManager serviceManager)
        {
            GlobalStore = globalStore;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.searchHandler = searchHandler;
            this.serviceQuery = serviceQuery;
            this.serviceManager = serviceManager;
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

            var globalStoreDataSet = GlobalStore.DataSets.Get(searchPrepareSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;

            var serviceSettings = new SearchSettingsWrapperElastic
            {
                DataSetName = globalStoreDataSet.DataSet.Name,
                ServiceId = service.Id
            };

            // SETUP default values for Activation - later here we can calculate more accurate settings for the dataset

            var defaultActivationSettings = new SearchActivateSettings();
            serviceSettings.Count = defaultActivationSettings.Count;

            var defaultAutoCompleteSettings = new AutoCompleteSettings();
            serviceSettings.AutoCompleteSettings = new AutoCompleteSettingsElastic
            {
                Confidence = defaultAutoCompleteSettings.Confidence,
                Count = defaultActivationSettings.Count,
                MaximumErrors = defaultAutoCompleteSettings.MaximumErrors
            };

            var defaultSearchSettings = new SearchSettings();
            serviceSettings.SearchSettings = new SearchSettingsElastic
            {
                Count = defaultActivationSettings.Count,
                CutOffFrequency = defaultSearchSettings.CutOffFrequency,
                Filter = null,
                Fuzziness = defaultSearchSettings.Fuzziness,
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

            var validationResult = Validate(searchActivateSettings.AutoCompleteSettings);
            if (validationResult != null) return validationResult;
            validationResult = Validate(searchActivateSettings.ClassifierSettings);
            if (validationResult != null) return validationResult;
            validationResult = Validate(searchActivateSettings.SearchSettings);
            if (validationResult != null) return validationResult;


            var searchSettings = serviceQuery.GetSettings<SearchSettingsWrapperElastic>(service.Id);
            service.Status = (int)ServiceStatusEnum.Active;

            if (searchActivateSettings != null)
            {
                searchSettings = MergeSettings(
                    searchSettings,
                    searchActivateSettings.AutoCompleteSettings,
                    searchActivateSettings.ClassifierSettings,
                    searchActivateSettings.SearchSettings,
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
            if (GlobalStore.ServiceAliases.IsExist(id)) id = GlobalStore.ServiceAliases.Get(id);

            var validationResult = serviceManager.ValidateIfServiceActive(id, ServiceTypeEnum.Search);
            if (validationResult != null) return validationResult;
            
            var searchSettings = MergeCounts(
                GlobalStore.ActivatedSearches.Get(id).SearchSettingsWrapper, 
                request.AutoCompleteCount, 
                request.ClassifierCount, 
                request.SearchCount);

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
            result.AutoCompleteResultList = searchResponse.Suggest?[DocumentQuery.SuggestName].SelectMany(s => s.Options).Where(o => o.CollateMatch).Select(o =>
                new AutoCompleteResult
                {
                    Text = o.Text,
                    Score = o.Score,
                }).ToList();

            // SEARCH
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
            AutoCompleteSettings autoCompleteSettings, ClassifierSettings classifierSettings, SearchSettings searchSettings, int count)
        {
            var result = new SearchSettingsWrapperElastic(defaultSettings);


            // all the settings are null so not tousch the default settings jut set the root parameters to them
            if (autoCompleteSettings == null && classifierSettings == null && searchSettings == null)
            {
                if (count == 0) return result;
                if (result.AutoCompleteSettings != null) result.AutoCompleteSettings.Count = count;
                if (result.SearchSettings != null) result.SearchSettings.Count = count;
                if (result.ClassifierSettings != null) result.ClassifierSettings.Count = count;
                return result;
            }

            // there are settings which are not null so apply all of the settings (along with the root parameters)
            if (autoCompleteSettings != null && count > 0 && autoCompleteSettings.Count == 0)
            {
                autoCompleteSettings.Count = count;
            }
            result.AutoCompleteSettings = autoCompleteSettings?.ToAutoCompleteSettingsElastic();

            if (searchSettings != null && count > 0 && searchSettings.Count == 0)
            {
                searchSettings.Count = count;
            }
            result.SearchSettings = searchSettings?.ToSearchSettingsElastic();

            if (classifierSettings != null && count > 0 && classifierSettings.Count == 0)
            {
                classifierSettings.Count = count;
            }
            result.ClassifierSettings = classifierSettings?.ToClassifierSearchSettingsElastic();

            return result;
        }

        private SearchSettingsWrapperElastic MergeCounts(
            SearchSettingsWrapperElastic defaultSettings, 
            int autoCompleteCount, int classifierCount, int searchCount)
        {
            var result = new SearchSettingsWrapperElastic(defaultSettings);

            if (result.AutoCompleteSettings != null && autoCompleteCount > 0)
            {
                result.AutoCompleteSettings.Count = autoCompleteCount;
            }

            if (result.ClassifierSettings != null && classifierCount > 0)
            {
                result.ClassifierSettings.Count = classifierCount;
            }

            if (result.SearchSettings != null && searchCount > 0)
            {
                result.SearchSettings.Count = searchCount;
            }
            return result;
        }

        private IActionResult Validate(AutoCompleteSettings autoCompleteSettings)
        {
            //nothing to do (the model validate everything)
            return null;
        }

        private IActionResult Validate(SearchSettings searchSettings)
        {
            //nothing to do (the model validate everything)
            return null;
        }

        private IActionResult Validate(ClassifierSettings classifierSettings)
        {
            if (classifierSettings == null) return null;
            return serviceManager.ValidateIfServiceActive(classifierSettings.Id, ServiceTypeEnum.Classifier);
        }
    }
}
