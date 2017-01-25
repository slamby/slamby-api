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
        readonly IDocumentService documentService;

        public IGlobalStoreManager GlobalStore { get; set; }

        public SearchController(ServiceQuery serviceQuery, SearchServiceHandler searchHandler, ProcessHandler processHandler,
            IQueryFactory queryFactory, IGlobalStoreManager globalStore, ServiceManager serviceManager, IDocumentService documentService)
        {
            GlobalStore = globalStore;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.searchHandler = searchHandler;
            this.serviceQuery = serviceQuery;
            this.serviceManager = serviceManager;
            this.documentService = documentService;
        }

        [HttpGet("{id}")]
        [SwaggerOperation("SearchGetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(SearchService))]
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

            // SETUP default values for Activation - here we can calculate more accurate settings for the dataset

            var defaultActivationSettings = new SearchActivateSettings();

            serviceSettings.AutoCompleteSettings = new AutoCompleteSettingsElastic
            {
                Confidence = 2.0,
                Count = 3,
                MaximumErrors = 0.5
            };

            serviceSettings.SearchSettings = new SearchSettingsElastic
            {
                Count = 3,
                CutOffFrequency = 0.001,
                Filter = null,
                Fuzziness = -1,
                ResponseFieldList = dataSet.InterpretedFields.Union(new List<string> { dataSet.IdField, dataSet.TagField }).ToList(),
                SearchFieldList = dataSet.InterpretedFields,
                Type = (int)SearchTypeEnum.Match,
                Weights = null,
                Operator = (int)LogicalOperatorEnum.OR,
                UseDefaultFilter = true,
                UseDefaultWeights = true,
                Order = null
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

            if (searchActivateSettings != null)
            {
                if (searchActivateSettings.AutoCompleteSettings != null)
                {
                    var validationResult = Validate(searchActivateSettings.AutoCompleteSettings);
                    if (validationResult != null) return validationResult;
                    searchSettings.AutoCompleteSettings = searchActivateSettings.AutoCompleteSettings.ToAutoCompleteSettingsElastic(searchSettings.AutoCompleteSettings);
                }
                if (searchActivateSettings.ClassifierSettings != null)
                {
                    var validationResult = Validate(searchActivateSettings.ClassifierSettings);
                    if (validationResult != null) return validationResult;
                    searchSettings.ClassifierSettings = searchActivateSettings.ClassifierSettings.ToClassifierSearchSettingsElastic(searchSettings.ClassifierSettings);
                    // there isn't default settings here at the prepare step so we have to set it up here
                    if (searchSettings.ClassifierSettings?.Count == 0) searchSettings.ClassifierSettings.Count = 3;
                }
                if (searchActivateSettings.SearchSettings != null)
                {
                    var validationResult = Validate(searchSettings.DataSetName, searchActivateSettings.SearchSettings);
                    if (validationResult != null) return validationResult;
                    searchSettings.SearchSettings = searchActivateSettings.SearchSettings.ToSearchSettingsElastic(searchSettings.SearchSettings);
                }
            }
            service.Status = (int)ServiceStatusEnum.Active;

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

            var defaultSettings = GlobalStore.ActivatedSearches.Get(id).SearchSettingsWrapper;

            if (request.AutoCompleteSettings != null)
            {
                validationResult = Validate(request.AutoCompleteSettings);
                if (validationResult != null) return validationResult;
            }
            if (request.ClassifierSettings != null)
            {
                validationResult = Validate(request.ClassifierSettings);
                if (validationResult != null) return validationResult;
            }
            if (request.SearchSettings != null)
            {
                validationResult = Validate(defaultSettings.DataSetName, request.SearchSettings);
                if (validationResult != null) return validationResult;
            }

            var searchSettings = MergeSettings(
                defaultSettings, 
                request.AutoCompleteSettings, 
                request.ClassifierSettings, 
                request.SearchSettings);

            var dataSet = GlobalStore.DataSets.Get(searchSettings.DataSetName);
            var result = new SearchResultWrapper();

            var documentQuery = queryFactory.GetDocumentQuery(dataSet.DataSet.Name);
            var searchResponse = documentQuery.Search(
                searchSettings.AutoCompleteSettings,
                searchSettings.SearchSettings,
                request.Text,
                dataSet.DocumentFields,
                dataSet.DataSet.TagField,
                dataSet.DataSet.InterpretedFields,
                defaultSettings.SearchSettings.Filter,
                defaultSettings.SearchSettings.Weights
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
            if (searchSettings.ClassifierSettings?.Count > 0)
            {
                var searchMatchCategories = (dataSet.TagIsArray
                    ? result.SearchResultList.SelectMany(d => ((Array)DocumentHelper.GetValue(d.Document, dataSet.DataSet.TagField)).Cast<string>())
                    : result.SearchResultList.Select(d => DocumentHelper.GetValue(d.Document, dataSet.DataSet.TagField).ToString()))
                .Distinct()
                .ToDictionary(t => t);

                var analyzeQuery = queryFactory.GetAnalyzeQuery(dataSet.DataSet.Name);
                var classifierId = searchSettings.ClassifierSettings.Id;
                var classifier = GlobalStore.ActivatedClassifiers.Get(GlobalStore.ServiceAliases.IsExist(classifierId) ? GlobalStore.ServiceAliases.Get(classifierId) : classifierId);
                //if the classifier is not activated right now
                if (classifier != null)
                {
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
                    result.ClassifierResultList = resultsList.Select(r => new SearchClassifierRecommendationResult
                    {
                        TagId = r.Key,
                        Score = r.Value,
                        Tag = classifier.ClassifiersTags[r.Key],
                        SearchResultMatch = searchMatchCategories.ContainsKey(r.Key)
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
                        ac.ClassifierResultList = resultsList.Select(r => new SearchClassifierRecommendationResult
                        {
                            TagId = r.Key,
                            Score = r.Value,
                            Tag = classifier.ClassifiersTags[r.Key],
                            SearchResultMatch = searchMatchCategories.ContainsKey(r.Key)
                        }).ToList();
                    }
                }
            }
            return new OkObjectResult(result);
        }

        private SearchSettingsWrapperElastic MergeSettings(
            SearchSettingsWrapperElastic defaultSettings,
            AutoCompleteSettings autoCompleteSettings, ClassifierSettings classifierSettings, SearchSettings searchSettings)
        {
            var result = new SearchSettingsWrapperElastic(defaultSettings);
            
            if (autoCompleteSettings != null)
            {
                result.AutoCompleteSettings = defaultSettings.AutoCompleteSettings != null ? autoCompleteSettings.ToAutoCompleteSettingsElastic(defaultSettings.AutoCompleteSettings) : null;
            }
            
            if (classifierSettings != null)
            {
                result.ClassifierSettings = defaultSettings.ClassifierSettings != null ? classifierSettings.ToClassifierSearchSettingsElastic(defaultSettings.ClassifierSettings) : null;
            }

            if (searchSettings != null)
            {
                result.SearchSettings = defaultSettings.SearchSettings != null ? searchSettings.ToSearchSettingsElastic(defaultSettings.SearchSettings) : null;
                if (searchSettings.UseDefaultFilter == false && searchSettings.Filter == null) result.SearchSettings.Filter = null;
                if (searchSettings.UseDefaultWeights == false && searchSettings.Weights == null) result.SearchSettings.Weights = null;
            }
            return result;
        }

        private IActionResult Validate(AutoCompleteSettings autoCompleteSettings)
        {
            //nothing to do (the model validate everything)
            return null;
        }

        private IActionResult Validate(string dataSetName, SearchSettings searchSettings)
        {
            if (searchSettings == null) return null;
            if (searchSettings.SearchFieldList != null)
            {
                var validateResult = documentService.ValidateFieldFilterFields(dataSetName, searchSettings.SearchFieldList);
                if (validateResult.IsFailure)
                {
                    return HttpErrorResult(StatusCodes.Status400BadRequest, validateResult.Error);
                }
            }
            if (searchSettings.ResponseFieldList != null)
            {
                var validateResult = documentService.ValidateFieldFilterFields(dataSetName, searchSettings.ResponseFieldList);
                if (validateResult.IsFailure)
                {
                    return HttpErrorResult(StatusCodes.Status400BadRequest, validateResult.Error);
                }
            }

            if (!string.IsNullOrEmpty(searchSettings.Order?.OrderByField))
            {
                var orderByFieldResult = documentService.ValidateOrderByField(dataSetName, searchSettings.Order.OrderByField);
                if (orderByFieldResult.IsFailure)
                {
                    return HttpErrorResult(StatusCodes.Status400BadRequest, orderByFieldResult.Error);
                }
            }

            return null;
        }

        private IActionResult Validate(ClassifierSettings classifierSettings)
        {
            if (classifierSettings == null) return null;
            if (string.IsNullOrEmpty(classifierSettings.Id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.IdCantBeEmptyIn_0_Settings, "Classifier")); 
            }
            return serviceManager.ValidateIfServiceActive(classifierSettings.Id, ServiceTypeEnum.Classifier);
        }
    }
}
