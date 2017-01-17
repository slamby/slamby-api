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
                DataSetName = globalStoreDataSet.IndexName,
                ServiceId = service.Id
            };

            // SETUP default values for Activation - later here we can calculate accurate settings for the dataset

            var defaultActivationSettings = new SearchActivateSettings();
            serviceSettings.Count = defaultActivationSettings.Count;
            serviceSettings.HighlightSettings = null;

            var defaultAutoCompleteSettings = new AutoCompleteSettings();
            serviceSettings.AutoCompleteSettings = new AutoCompleteSettingsElastic {
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
                SearchFieldWeights = null,
                Type = (int)SearchTypeEnum.Match,
                Weights = null
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
    }
}
