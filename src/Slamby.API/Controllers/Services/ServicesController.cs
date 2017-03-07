using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Services;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using Microsoft.Extensions.DependencyInjection;
using Slamby.API.Helpers.Swashbuckle;
using Swashbuckle.SwaggerGen.Annotations;
using Slamby.API.Helpers;
using Slamby.API.Services.Interfaces;
using Slamby.API.Filters;
using Slamby.Common.Helpers;
using Slamby.API.Resources;
using Slamby.Common.Config;

namespace Slamby.API.Controllers
{
    [Route("api/Services")]
    [SwaggerGroup("Service")]
    [SwaggerResponseRemoveDefaults]
    public class ServicesController : BaseController
    {
        readonly ServiceQuery serviceQuery;
        readonly ServiceHandler serviceHandler;

        public IGlobalStoreManager GlobalStore { get; set; }

        public ServicesController(ServiceQuery serviceQuery, IGlobalStoreManager globalStore, ServiceHandler serviceHandler)
        {
            GlobalStore = globalStore;
            this.serviceQuery = serviceQuery;
            this.serviceHandler = serviceHandler;
        }

        [HttpGet]
        [SwaggerOperation("GetServices")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<Service>))]
        public IActionResult Get()
        {
            var services = serviceHandler.GetAll();
            return new OkObjectResult(services);
        }

        [HttpGet("{id}", Name = "GetService")]
        [SwaggerOperation("GetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Service))]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Get(string id)
        {
            var service = serviceHandler.Get(id);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }
            return new OkObjectResult(service);
        }

        [HttpPost]
        [SwaggerOperation("CreateService")]
        [SwaggerResponse(StatusCodes.Status201Created)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        public IActionResult Post([FromBody]Service service)
        {
            if (!string.IsNullOrEmpty(service.Id))
            {
                return new StatusCodeResult(StatusCodes.Status400BadRequest);
            }
            if (string.IsNullOrEmpty(service.Name))
            {
                return new StatusCodeResult(StatusCodes.Status400BadRequest);
            }
            if (!Enum.IsDefined(typeof(ServiceTypeEnum), service.Type))
            {
                return new StatusCodeResult(StatusCodes.Status400BadRequest);
            }

            var serviceElastic = ServiceElastic.Create(service.Name, service.Type, service.Alias, service.Description);

            RemoveServiceAlias(serviceElastic.Alias);
            serviceQuery.Index(serviceElastic);

            GlobalStore.ServiceAliases.Set(serviceElastic.Alias, serviceElastic.Id);

            var serviceModel = serviceElastic.ToServiceModel<Service>();

            return base.CreatedAtRoute("GetService", new { Controller = "Services", id = serviceElastic.Id }, serviceModel);
        }

        private void RemoveServiceAlias(string alias)
        {
            var serviceElastic = serviceQuery.GetByAlias(alias);
            if (serviceElastic != null)
            {
                serviceElastic.Alias = null;
                serviceQuery.Index(serviceElastic);
            }
        }

        [HttpPut("{id}")]
        [SwaggerOperation("UpdateService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Service))]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        public IActionResult Put(string id, [FromBody]Service service)
        {
            var serviceOrig = serviceHandler.Get(id);
            if (serviceOrig == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }

            if (!string.IsNullOrEmpty(service.Name))
            {
                serviceOrig.Name = service.Name;
            }
            if (!string.IsNullOrEmpty(service.Description))
            {
                serviceOrig.Description = service.Description;
            }
            if (!string.IsNullOrEmpty(service.Alias))
            {
                RemoveServiceAlias(service.Alias);
                serviceOrig.Alias = service.Alias;
            }

            serviceHandler.Index(serviceOrig);

            GlobalStore.ServiceAliases.Set(serviceOrig.Alias, serviceOrig.Id);

            return new StatusCodeResult(StatusCodes.Status200OK);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation("DeleteService")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Delete(string id, [FromServices]IServiceProvider serviceProvider, [FromServices]ProcessQuery processQuery, [FromServices]SiteConfig siteConfig)
        {
            var service = serviceHandler.Get(id, true);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }

            if (siteConfig.AvailabilityConfig.ClusterSize > 1 && service.Status == ServiceStatusEnum.Busy)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusInClusterModeOnlyNotBusyCanDelete);
            }

            switch (service.Type)
            {
                case ServiceTypeEnum.Classifier:
                    var classifierHandler = serviceProvider.GetService<ClassifierServiceHandler>();
                    classifierHandler.Delete(service);
                    serviceQuery.DeleteSettings<ClassifierSettingsElastic>(service.Id);
                    break;
                case ServiceTypeEnum.Prc:
                    var prcHandler = serviceProvider.GetService <PrcServiceHandler>();
                    prcHandler.Delete(service);
                    serviceQuery.DeleteSettings<PrcSettingsElastic>(service.Id);
                    break;
                case ServiceTypeEnum.Search:
                    var searchHandler = serviceProvider.GetService<SearchServiceHandler>();
                    searchHandler.Delete(service);
                    serviceQuery.DeleteSettings<SearchSettingsWrapperElastic>(service.Id);
                    break;
            }

            serviceQuery.Delete(service.Id);

            GlobalStore.ServiceAliases.Remove(service.Alias);

            if (service.ProcessIdList != null)
            {
                processQuery.Delete(service.ProcessIdList);
            }

            return new StatusCodeResult(StatusCodes.Status200OK);
        }
    }
}
