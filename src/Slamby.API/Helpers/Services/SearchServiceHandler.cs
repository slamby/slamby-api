using Slamby.API.Helpers.Services.Interfaces;
using Slamby.API.Models;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class SearchServiceHandler : ITypedServiceHandler<SearchSettingsWrapperElastic>
    {
        readonly SiteConfig siteConfig;
        readonly ServiceQuery serviceQuery;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;
        readonly ParallelService parallelService;
        readonly MachineResourceService machineResourceService;
        readonly SearchRedisHandler searchRedisHandler;
        readonly ServiceHandler serviceHandler;

        public IGlobalStoreManager GlobalStore { get; set; }

        public SearchServiceHandler(SiteConfig siteConfig, ServiceQuery serviceQuery, ProcessHandler processHandler,
            IQueryFactory queryFactory, ParallelService parallelService, MachineResourceService machineResourceService,
            IGlobalStoreManager globalStore, SearchRedisHandler searchRedisHandler, ServiceHandler serviceHandler)
        {
            GlobalStore = globalStore;
            this.parallelService = parallelService;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.serviceQuery = serviceQuery;
            this.siteConfig = siteConfig;
            this.machineResourceService = machineResourceService;
            this.searchRedisHandler = searchRedisHandler;
            this.serviceHandler = serviceHandler;
        }

        public SearchService Get(string id, bool withSettings = true)
        {
            var service = serviceHandler.Get<SearchService>(id);
            if (service == null) return null;

            SearchActivateSettings activateSettings = null;
            SearchPrepareSettings prepareSettings = null;

            var searchSettingsElastic = withSettings ? serviceQuery.GetSettings<SearchSettingsWrapperElastic>(service.Id) : null;
            if (searchSettingsElastic != null)
            {
                prepareSettings = searchSettingsElastic.ToSearchPrepareSettingsModel();
                activateSettings = searchSettingsElastic.ToSearchActivateSettingsModel();
            }

            service.ActivateSettings = activateSettings;
            service.PrepareSettings = prepareSettings;

            return service;
        }

        public void Prepare(string processId, SearchSettingsWrapperElastic settings, CancellationToken token)
        {
            try
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Busy;
                serviceHandler.Update(service.Id, service);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyPrepared_0_Service_1, ServiceTypeEnum.Classifier, service.Name));
                service.Status = ServiceStatusEnum.Prepared;
                serviceHandler.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.New;
                serviceHandler.Update(service.Id, service);
                if (ex.InnerException != null && ex.InnerException is OperationCanceledException)
                {
                    processHandler.Cancelled(processId);
                }
                else
                {
                    processHandler.Interrupted(processId, ex);
                }
            }
        }

        public void Activate(string processId, SearchSettingsWrapperElastic settings, CancellationToken token)
        {
            try
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Busy;
                serviceHandler.Update(service.Id, service);

                var globalStoreSearch = new GlobalStoreSearch {
                    SearchSettingsWrapper = settings
                };

                GlobalStore.ActivatedSearches.Add(settings.ServiceId, globalStoreSearch);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyActivated_0_Service_1, ServiceTypeEnum.Search, service.Name));
                service.Status = ServiceStatusEnum.Active;
                serviceHandler.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Prepared;
                serviceHandler.Update(service.Id, service);
                if (GlobalStore.ActivatedSearches.IsExist(settings.ServiceId)) GlobalStore.ActivatedSearches.Remove(settings.ServiceId);
                if (ex.InnerException != null && ex.InnerException is OperationCanceledException)
                {
                    processHandler.Cancelled(processId);
                }
                else
                {
                    processHandler.Interrupted(processId, ex);
                }
                GC.Collect();
            }
        }

        public void Deactivate(string serviceId)
        {
            GlobalStore.ActivatedSearches.Remove(serviceId);
            GC.Collect();
        }

        public void Delete(Service service)
        {
            if (service.Status == ServiceStatusEnum.Active)
            {
                Deactivate(service.Id);
            }
            if (service.Status == ServiceStatusEnum.Busy)
            {
                var actualProcessId = service.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
                if (actualProcessId != null)
                {
                    processHandler.Cancel(actualProcessId);
                    while (GlobalStore.Processes.IsExist(actualProcessId)) { }
                }

            }
            if (service.Status == ServiceStatusEnum.Prepared) { }
        }


        public void SaveSearchRequest(SearchSettingsWrapperElastic settings, SearchRequest request)
        {
            searchRedisHandler.SaveSearch(settings.ServiceId, request.Text);
        }

    }
}
