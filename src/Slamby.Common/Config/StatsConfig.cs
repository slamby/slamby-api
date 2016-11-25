using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.Common.Config
{
    public class StatsConfig
    {
        public RedisConfig Redis { get; set; }

        public bool Enabled { get; set; }

        public class RedisConfig
        {
            public string Configuration { get; set; }
            public Dictionary<string,string> CommandMap { get; set; }
        }
    }
    
}
