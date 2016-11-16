using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Slamby.API.Services;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Elastic.Factories;

namespace Slamby.API.Helpers
{
    [SingletonDependency]
    public class StartupService
    {
        readonly SiteConfig siteConfig;

        readonly ElasticClientFactory elasticClientFactory;
        readonly ILogger<StartupService> logger;
        readonly DataSetService dataSetService;
        readonly DBUpdateService dbUpdateService;
        readonly MachineResourceService machineResourceService;
        readonly ServiceManager serviceManager;
        readonly ILicenseManager serverIdManager;

        public StartupService(SiteConfig siteConfig, ILogger<StartupService> logger,
            ElasticClientFactory elasticClientFactory, DataSetService dataSetService, 
            DBUpdateService dbUpdateService, MachineResourceService machineResourceService,
            ServiceManager serviceManager, ILicenseManager serverIdManager)
        {
            this.serverIdManager = serverIdManager;
            this.serviceManager = serviceManager;
            this.dbUpdateService = dbUpdateService;
            this.dataSetService = dataSetService;
            this.logger = logger;
            this.elasticClientFactory = elasticClientFactory;
            this.siteConfig = siteConfig;
            this.machineResourceService = machineResourceService;
        }

        public void Startup()
        {
            logger.LogInformation("Starting up...");

            logger.LogInformation($"MaxIndexBulkSize set to {siteConfig.Resources.MaxIndexBulkSize} byte");
            logger.LogInformation($"MaxIndexBulkCount set to {siteConfig.Resources.MaxIndexBulkCount}");
            logger.LogInformation($"MaxSearchBulkCount set to {siteConfig.Resources.MaxSearchBulkCount}");

            CreateDirectories();
            serverIdManager.EnsureAppIdCreated();

            logger.LogInformation("Waiting ElasticSearch to start...");
            WaitForElastic();

            InitMachineResources();

            serviceManager.CreateServiceIndexes();
            dbUpdateService.UpdateDatabase();

            dataSetService.LoadGlobalStore();

            serviceManager.LoadGlobalStore();
            serviceManager.MaintainBusyServices();
            serviceManager.CancelBusyProcesses();
            serviceManager.WarmUpServices();

            logger.LogInformation("Startup finished");
        }

        private void WaitForElastic()
        {
            var available = false;
            var counter = 1;

            do
            {
                var isValid = false;
                var status = string.Empty;

                try
                {
                    var client = elasticClientFactory.GetClient();
                    var health = client.ClusterHealth();
                    isValid = health.IsValid;
                    status = health.Status;
                }
                catch
                {
                    isValid = false;
                }

                if (isValid)
                {
                    if (status != null && !string.Equals(status, "red", StringComparison.OrdinalIgnoreCase))
                    {
                        available = true;
                        logger.LogInformation($"Elasticsearch connection is Available ({status})");
                        break;
                    }

                    logger.LogInformation($"Elasticsearch connection is Online ({counter}) ({status})");
                }
                else
                {
                    logger.LogWarning($"Elasticsearch connection is Offline ({counter})");
                }

                System.Threading.Thread.Sleep(2000);
            }
            while ((counter++) < 300);

            if (!available)
            {
                throw new TimeoutException("Elasticsearch is Offline! Check ES settings!");
            }
        }

        private void CreateDirectories()
        {
            var dirs = new List<string>()
            {
                siteConfig.Directory.Classifier,
                siteConfig.Directory.Prc,
                siteConfig.Directory.User,
                siteConfig.Directory.Temp,
                siteConfig.Directory.Sys
            };

            dirs.ForEach((dir) => Directory.CreateDirectory(dir));
        }

        private void InitMachineResources()
        {
            // Gets the latest values
            machineResourceService.UpdateResourcesManually();

            // Starts background updater thread
            machineResourceService.StartBackgroundUpdater();
        }
    }
}
