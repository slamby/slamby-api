using Slamby.API.Services.Interfaces;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class ServiceHandler
    {
        readonly ServiceQuery serviceQuery;
        readonly ProcessQuery processQuery;
        readonly bool isCluster;

        public IGlobalStoreManager GlobalStore { get; set; }

        public ServiceHandler(ServiceQuery serviceQuery, IGlobalStoreManager globalStore, ProcessQuery processQuery, SiteConfig siteConfig)
        {
            this.serviceQuery = serviceQuery;
            this.processQuery = processQuery;
            this.GlobalStore = globalStore;
            this.isCluster = siteConfig.AvailabilityConfig.ClusterSize > 1;
        }

        public Service Get(string idOrAlias, bool keepOriginalStatus = false)
        {
            return Get<Service>(idOrAlias, keepOriginalStatus);
        }

        public T Get<T>(string idOrAlias, bool keepOriginalStatus = false) where T : Service, new()
        {
            string id;
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(idOrAlias))
            {
                id = GlobalStore.ServiceAliases.Get(idOrAlias);
            } else
            {
                id = idOrAlias;
            }
            var serviceElastic = serviceQuery.Get(id);
            if (serviceElastic == null) return null;

            return Convert<T>(serviceElastic, keepOriginalStatus);
        }

        public List<Service> GetAll()
        {
            return GetAll<Service>();
        }

        public List<T> GetAll<T>(bool keepOriginalStatus = false) where T : Service, new()
        {
            var serviceElastics = serviceQuery.GetAll();
            return serviceElastics.Select(s => Convert<T>(s, keepOriginalStatus)).ToList();
        }

        public List<T> GetByType<T>(ServiceTypeEnum type, bool keepOriginalStatus = false) where T : Service, new()
        {
            var serviceElastics = serviceQuery.GetByType((int)type);
            return serviceElastics.Select(s => Convert<T>(s, keepOriginalStatus)).ToList();
        }

        public string Update(string id, Service service)
        {
            return serviceQuery.Update(id, service.ToServiceElastic());
        }

        public void Index(Service service)
        {
            serviceQuery.Index(service.ToServiceElastic());
        }

        private T Convert<T> (ServiceElastic serviceElastic, bool keepOriginalStatus) where T : Service, new()
        {
            var service = serviceElastic.ToServiceModel<T>();
            service.ActualProcessId = serviceElastic.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));

            if (!isCluster || keepOriginalStatus || service.Status == ServiceStatusEnum.New) return service;

            var existsInGlobalStore =
                (service.Type == ServiceTypeEnum.Classifier && GlobalStore.ActivatedClassifiers.IsExist(service.Id)) ||
                (service.Type == ServiceTypeEnum.Prc && GlobalStore.ActivatedPrcs.IsExist(service.Id)) ||
                (service.Type == ServiceTypeEnum.Search && GlobalStore.ActivatedSearches.IsExist(service.Id));

            // if the GlobalStore contains the service then the service will be in Active status
            // if the status is Active but the service is missing from the GlobalStore then it will be prepared
            if (service.Status == ServiceStatusEnum.Prepared || service.Status == ServiceStatusEnum.Active)
            {
                service.Status = existsInGlobalStore ? ServiceStatusEnum.Active : ServiceStatusEnum.Prepared;
                return service;
            }

            // if the service is busy and the ActualProcessId is null, then the service is busy on an other cluster member
            if (service.Status == ServiceStatusEnum.Busy && service.ActualProcessId == null)
            {
                var lastProcess = processQuery.GetAll(true, 0, service.ProcessIdList, true).OrderByDescending(p => p.Start).FirstOrDefault();
                if (lastProcess == null) return service;

                // it the actual process is not a preparation then it's an already prepared service
                if (lastProcess.Type != (int)ProcessTypeEnum.ClassifierPrepare &&
                    lastProcess.Type != (int)ProcessTypeEnum.PrcPrepare &&
                    lastProcess.Type != (int)ProcessTypeEnum.SearchPrepare)
                {
                    service.Status = ServiceStatusEnum.Prepared;
                }
            }
            return service;
        }
    }
}
