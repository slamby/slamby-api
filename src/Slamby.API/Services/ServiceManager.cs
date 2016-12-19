using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Services;
using Slamby.API.Helpers.Services.Interfaces;
using Slamby.API.Services.Interfaces;
using Slamby.Common.DI;
using Slamby.Common.Exceptions;
using Slamby.Common.Helpers;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;

namespace Slamby.API.Services
{
    [ScopedDependency]
    public class ServiceManager
    {
        readonly ServiceQuery serviceQuery;
        readonly IServiceProvider serviceProvider;
        readonly ProcessQuery processQuery;
        readonly ProcessHandler processHandler;

        public IGlobalStoreManager GlobalStore { get; set; }

        readonly DataSetService dataSetService;
        readonly ILogger<ServiceManager> logger;
        readonly PrcServiceHandler prcServiceHandler;
        readonly PrcIndexServiceHandler prcIndexServiceHandler;

        public ServiceManager(ServiceQuery serviceQuery, IServiceProvider serviceProvider, ProcessQuery processQuery,
            ProcessHandler processHandler, IGlobalStoreManager globalStore, DataSetService dataSetService,
            ILogger<ServiceManager> logger, PrcServiceHandler prcServiceHandler, PrcIndexServiceHandler prcIndexServiceHandler)
        {
            this.prcIndexServiceHandler = prcIndexServiceHandler;
            this.prcServiceHandler = prcServiceHandler;
            this.logger = logger;
            this.dataSetService = dataSetService;
            GlobalStore = globalStore;
            this.processHandler = processHandler;
            this.processQuery = processQuery;
            this.serviceProvider = serviceProvider;
            this.serviceQuery = serviceQuery;
        }

        public void CreateServiceIndexes()
        {
            foreach (var service in serviceProvider.GetServices<IEnsureIndex>())
            {
                service.CreateIndex();
            }
        }

        /// <summary>
        /// Warms up the activated Classifier, Prc services
        /// </summary>
        public void WarmUpServices()
        {
            WarmUpService<PrcSettingsElastic, PrcServiceHandler>(ServiceTypeEnum.Prc);
            WarmUpService<ClassifierSettingsElastic, ClassifierServiceHandler>(ServiceTypeEnum.Classifier);
        }

        private void WarmUpService<TServiceSettings, THandler>(ServiceTypeEnum serviceType)
            where TServiceSettings : BaseServiceSettingsElastic
            where THandler : IServiceHandler<TServiceSettings>
        {
            var services = serviceQuery.GetByType((int)serviceType).ToList();

            var handler = serviceProvider.GetService<THandler>();
            foreach (var service in services)
            {
                ProcessTypeEnum processType;
                switch (serviceType)
                {
                    case ServiceTypeEnum.Classifier:
                        processType = ProcessTypeEnum.ClassifierActivate;
                        break;
                    case ServiceTypeEnum.Prc:
                        processType = ProcessTypeEnum.PrcActivate;
                        break;
                    default:
                        throw new Exception("Try to warm up service with undefined process activation type!");
                }

                if (service.Status != (int)ServiceStatusEnum.Active)
                {
                    continue;
                }

                var settings = serviceQuery.GetSettings<TServiceSettings>(service.Id);
                var process = processHandler.Create(
                    processType,
                    service.Id,
                    service,
                    string.Format(Resources.ServiceResources.Activating_0_Service_1, serviceType, service.Name));

                service.ProcessIdList.Add(process.Id);
                serviceQuery.Update(service.Id, service);

                processHandler.Start(process, (tokenSource) => handler.Activate(process.Id, settings, tokenSource.Token));
            }
        }

        public void UpdateDataSetNameToIndex<TServiceSettings>(ServiceTypeEnum serviceType)
            where TServiceSettings : BaseServiceSettingsElastic
        {
            var dataSets = dataSetService.GetDataSetIndexAlias();

            foreach (var service in serviceQuery.GetByType((int)serviceType))
            {
                var settings = serviceQuery.GetSettings<TServiceSettings>(service.Id);
                if (settings == null)
                {
                    continue;
                }

                var key = dataSets
                    .Where(d => d.Value.EqualsOrdinalIgnoreCase(settings.DataSetName)) // search for aliases
                    .Select(d => d.Key)
                    .FirstOrDefault();
                if (key == null)
                {
                    continue;
                }

                // if alias is used then replace with index name
                settings.DataSetName = key;
                serviceQuery.IndexSettings<TServiceSettings>(settings);
            }
        }

        public void LoadGlobalStore()
        {
            foreach (var service in serviceQuery.GetAll().Where(s => !string.IsNullOrEmpty(s.Alias)))
            {
                GlobalStore.ServiceAliases.Set(service.Alias, service.Id);
            }
        }

        private ProcessTypeEnum? GetProcessTypeForServiceType(ServiceTypeEnum serviceType)
        {
            switch (serviceType)
            {
                case ServiceTypeEnum.Classifier:
                    return ProcessTypeEnum.ClassifierActivate;
                case ServiceTypeEnum.Prc:
                    return ProcessTypeEnum.PrcActivate;
                default:
                    return null;
            }
        }

        public void CancelBusyProcesses()
        {
            var busyProcesses = processQuery.GetAll(true);
            foreach (var process in busyProcesses)
            {
                if (process.Type == (int)ProcessTypeEnum.PrcIndex)
                {
                    var serviceId = process.AffectedObjectId?.ToString();
                    var service = serviceQuery.Get(serviceId);
                    if (service == null)
                    {
                        logger.LogError($"Cannot find Service {serviceId} of the interrupted process {process.Id}");
                        continue;
                    }

                    prcIndexServiceHandler.CleanPrcIndex(serviceId);
                }

                processHandler.Interrupted(process.Id, new SlambyException(Resources.ProcessResources.UnexpectedInterruptionError));
            }
        }

        public void MaintainBusyServices()
        {
            var busyServices = serviceQuery.GetAll().Where(s => s.Status == (int)ServiceStatusEnum.Busy).ToList();

            var classifierHandler = serviceProvider.GetService<ClassifierServiceHandler>();
            var prcHandler = serviceProvider.GetService<PrcServiceHandler>();

            foreach (var service in busyServices)
            {
                var lastProcess = processQuery.Get(service.ProcessIdList.Last());

                //if the last process was a preparation then the service will be in New status
                if (lastProcess.Type == (int)ProcessTypeEnum.ClassifierPrepare ||
                    lastProcess.Type == (int)ProcessTypeEnum.PrcPrepare) service.Status = (int)ServiceStatusEnum.New;

                //if the last process was an activation then the service will be in Activated status (so the API can Activate the service)
                else if (lastProcess.Type == (int)ProcessTypeEnum.ClassifierActivate ||
                            lastProcess.Type == (int)ProcessTypeEnum.PrcActivate) service.Status = (int)ServiceStatusEnum.Active;

                //otherwise it will be New
                else service.Status = (int)ServiceStatusEnum.New;

                serviceQuery.Update(service.Id, service);

                //if a preparation failed, then we need to delete the dictionaries directory (maybe in the future we can continue it...)
                if (service.Status == (int)ServiceStatusEnum.New)
                {
                    var dirPath = "";
                    if (service.Type == (int)ServiceTypeEnum.Classifier)
                    {
                        dirPath = classifierHandler.GetDirectoryPath(service.Id);
                    }
                    if (service.Type == (int)ServiceTypeEnum.Prc)
                    {
                        dirPath = prcHandler.GetDirectoryPath(service.Id);
                    }
                    IOHelper.SafeDeleteDictionary(dirPath, true);
                }
            }
        }
    }
}
