using Slamby.API.Services.Interfaces;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.SDK.Net;
using Slamby.SDK.Net.Managers;
using Slamby.SDK.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(IClusterClient))]
    public class ClusterClient : IClusterClient
    {
        private Dictionary<string, Configuration> sdkConfigurationDic = new Dictionary<string, Configuration>();
        private ISecretManager secretManager;

        public ClusterClient(ISecretManager secretManager)
        {
            this.secretManager = secretManager;
        }

        public void Init(List<ClusterMember> clusterMembers)
        {
            foreach(var clusterMember in clusterMembers)
            {
                sdkConfigurationDic.Add(clusterMember.Id,
                    new Configuration
                    {
                        ApiBaseEndpoint = new Uri(clusterMember.Address),
                        ApiSecret = secretManager.GetSecret(),
                        Timeout = TimeSpan.FromSeconds(30)
                    });
            }
        }

        public async Task<ClientResponseWithObject<Status>> GetStatus(string id)
        {
            var statusManager = new StatusManager(sdkConfigurationDic[id]);
            return await statusManager.GetStatusAsync();
        }


    }
}
