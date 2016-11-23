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

namespace Slamby.API.Controllers
{
    [Route("api/Services")]
    [SwaggerGroup("Service")]
    [SwaggerResponseRemoveDefaults]
    public class ServicesController : BaseController
    {
        readonly ServiceQuery serviceQuery;

        public IGlobalStoreManager GlobalStore { get; set; }

        public ServicesController(ServiceQuery serviceQuery, IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.serviceQuery = serviceQuery;
        }

        [HttpGet]
        [SwaggerOperation("GetServices")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<Service>))]
        public IActionResult Get()
        {
            var serviceElastics = serviceQuery.GetAll();
            var services = serviceElastics.Select(s => 
            {
                var service = s.ToServiceModel<Service>();
                service.ActualProcessId = s.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
                return service;
            });

            return new OkObjectResult(services);
        }

        [HttpGet("{id}", Name = "GetService")]
        [SwaggerOperation("GetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Service))]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Get(string id)
        {
            var serviceElastic = serviceQuery.Get(id);
            if (serviceElastic == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }
            var service = serviceElastic.ToServiceModel<Service>();
            service.ActualProcessId = serviceElastic.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
            return new OkObjectResult(service);
        }

        [HttpPost]
        [SwaggerOperation("CreateService")]
        [SwaggerResponse(StatusCodes.Status201Created)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
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
            serviceModel.ActualProcessId = serviceElastic.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));

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
        public IActionResult Put(string id, [FromBody]Service service)
        {
            var serviceElastic = serviceQuery.Get(id);
            if (serviceElastic == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }

            if (!string.IsNullOrEmpty(service.Name))
            {
                serviceElastic.Name = service.Name;
            }
            if (!string.IsNullOrEmpty(service.Description))
            {
                serviceElastic.Description = service.Description;
            }
            if (!string.IsNullOrEmpty(service.Alias))
            {
                RemoveServiceAlias(service.Alias);
                serviceElastic.Alias = service.Alias;
            }

            serviceQuery.Index(serviceElastic);

            GlobalStore.ServiceAliases.Set(serviceElastic.Alias, serviceElastic.Id);

            return new StatusCodeResult(StatusCodes.Status200OK);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation("DeleteService")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Delete(string id, [FromServices]IServiceProvider serviceProvider, [FromServices]ProcessQuery processQuery)
        {
            var service = serviceQuery.Get(id);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }

            switch (service.Type)
            {
                case (int)ServiceTypeEnum.Classifier:
                    var classifierHandler = serviceProvider.GetService<ClassifierServiceHandler>();
                    classifierHandler.Delete(service);
                    serviceQuery.DeleteSettings<ClassifierSettingsElastic>(service.Id);
                    break;
                case (int)ServiceTypeEnum.Prc:
                    var prcHandler = serviceProvider.GetService <PrcServiceHandler>();
                    prcHandler.Delete(service);
                    serviceQuery.DeleteSettings<PrcSettingsElastic>(service.Id);
                    break;
            }

            serviceQuery.Delete(service);

            GlobalStore.ServiceAliases.Remove(service.Alias);

            if (service.ProcessIdList != null)
            {
                processQuery.Delete(service.ProcessIdList);
            }

            return new StatusCodeResult(StatusCodes.Status200OK);
        }
    }
}
