using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.Common.Config
{
    public class AvailabilityConfig
    {
        public List<ClusterMember> ClusterPartners { get; set; } = new List<ClusterMember>();
        public int ClusterSize { get; set; } = 1;

    }

    public class ClusterMember
    {
        public string Id { get; set; }
        public string Address { get; set; }
    }
}
