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
    public class SearchServiceHandler : IServiceHandler<SearchSettingsWrapperElastic>
    {
        readonly SiteConfig siteConfig;
        readonly ServiceQuery serviceQuery;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;
        readonly ParallelService parallelService;
        readonly MachineResourceService machineResourceService;
        readonly SearchRedisHandler searchRedisHandler;

        public IGlobalStoreManager GlobalStore { get; set; }

        public SearchServiceHandler(SiteConfig siteConfig, ServiceQuery serviceQuery, ProcessHandler processHandler,
            IQueryFactory queryFactory, ParallelService parallelService, MachineResourceService machineResourceService,
            IGlobalStoreManager globalStore, SearchRedisHandler searchRedisHandler)
        {
            GlobalStore = globalStore;
            this.parallelService = parallelService;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.serviceQuery = serviceQuery;
            this.siteConfig = siteConfig;
            this.machineResourceService = machineResourceService;
            this.searchRedisHandler = searchRedisHandler;
        }

        public void Prepare(string processId, SearchSettingsWrapperElastic settings, CancellationToken token)
        {
            try
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Busy;
                serviceQuery.Update(service.Id, service);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyPrepared_0_Service_1, ServiceTypeEnum.Classifier, service.Name));
                service.Status = (int)ServiceStatusEnum.Prepared;
                serviceQuery.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.New;
                serviceQuery.Update(service.Id, service);
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
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Busy;
                serviceQuery.Update(service.Id, service);

                var globalStoreSearch = new GlobalStoreSearch {
                    SearchSettingsWrapper = settings
                };

                GlobalStore.ActivatedSearches.Add(settings.ServiceId, globalStoreSearch);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyActivated_0_Service_1, ServiceTypeEnum.Search, service.Name));
                service.Status = (int)ServiceStatusEnum.Active;
                serviceQuery.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Prepared;
                serviceQuery.Update(service.Id, service);
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

        public void Delete(ServiceElastic serviceElastic)
        {
            if (serviceElastic.Status == (int)ServiceStatusEnum.Active)
            {
                Deactivate(serviceElastic.Id);
            }
            if (serviceElastic.Status == (int)ServiceStatusEnum.Busy)
            {
                var actualProcessId = serviceElastic.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
                if (actualProcessId != null)
                {
                    processHandler.Cancel(actualProcessId);
                    while (GlobalStore.Processes.IsExist(actualProcessId)) { }
                }

            }
            if (serviceElastic.Status == (int)ServiceStatusEnum.Prepared) { }
        }


        public void SaveSearchRequest(SearchSettingsWrapperElastic settings, SearchRequest request)
        {
            searchRedisHandler.SaveSearch(settings.ServiceId, request.Text);
        }

    }
}
