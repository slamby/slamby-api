using Slamby.Common.Config;
using Slamby.SDK.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    public interface IClusterClient
    {
        void Init(List<ClusterMember> clusterMembers);

        Task<ClientResponseWithObject<Status>> GetStatus(string id);
    }
}
