using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Slamby.Common.Config;
using Slamby.Common.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(IClusterManager))]
    public class ClusterManager : IClusterManager
    {
        private SiteConfig SiteConfig;
        private CancellationToken StopToken;
        private IClusterClient ClusterClient;
        private List<string> ClusterPartnerIds;
        private Dictionary<string, ClusterMemberHealth> ClusterPartnersHealthDic;
        readonly ILogger<ClusterManager> logger;


        public ClusterManager(SiteConfig siteConfig, IApplicationLifetime applicationLifetime, IClusterClient clusterClient, ILogger<ClusterManager> logger)
        {
            SiteConfig = siteConfig;
            StopToken = applicationLifetime.ApplicationStopping;
            ClusterClient = clusterClient;
            ClusterPartnerIds = SiteConfig.AvailabilityConfig.ClusterPartners.Select(p => p.Id).ToList();
            ClusterPartnersHealthDic = SiteConfig.AvailabilityConfig.ClusterPartners.ToDictionary(d => d.Id, d => new ClusterMemberHealth {
                Id = d.Id
            });
            this.logger = logger;

            clusterClient.Init(SiteConfig.AvailabilityConfig.ClusterPartners);
        }

        public void StartBackgroundMembersCheck()
        {
            new TaskFactory()
                .StartNew(
                    async () => { await MembersCheckPeriodically(); },
                    TaskCreationOptions.LongRunning);
        }

        private async Task MembersCheckPeriodically()
        {
            while (!StopToken.IsCancellationRequested)
            {
                foreach (var id in ClusterPartnerIds)
                {
                    var dateTime = DateTime.UtcNow;
                    try
                    {
                        var status = await ClusterClient.GetStatus(id);
                        ClusterPartnersHealthDic[id].IsReachable = status.IsSuccessful;
                        ClusterPartnersHealthDic[id].LastCheck = dateTime;
                        if (status.IsSuccessful)
                        {
                            ClusterPartnersHealthDic[id].LastSuccessfulCheck = dateTime;
                            logger.LogInformation($"Cluster Partner {id} status is OK");
                        } else
                        {
                            logger.LogInformation($"Cluster Partner {id} statis is NOT OK");
                        }
                    } catch (Exception ex)
                    {
                        ClusterPartnersHealthDic[id].IsReachable = false;
                        ClusterPartnersHealthDic[id].LastCheck = dateTime;
                        logger.LogError("Cluster Partner Check Error", ex);
                    }
                }
                try
                {
                    Task.Delay(TimeSpan.FromSeconds(10))
                        .Wait(StopToken);
                }
                catch (OperationCanceledException)
                {
                    // If token cancelled OperationCanceledException is thrown, but it is expected exception
                    break;
                }
            }
        }
    }

    public class ClusterMemberHealth
    {
        public string Id { get; set; }
        public bool IsReachable { get; set; }
        public DateTime LastCheck { get; set; }
        public DateTime LastSuccessfulCheck { get; set; }
    }

}
