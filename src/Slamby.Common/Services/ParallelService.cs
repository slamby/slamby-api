using System;
using System.Linq;
using System.Threading.Tasks;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Microsoft.AspNetCore.Http;

namespace Slamby.Common.Services
{
    [TransientDependency]
    public class ParallelService
    {
        int maximumValue = Environment.ProcessorCount;

        public int MaximumValue
        {
            get
            {
                return maximumValue;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumValue));
                }

                maximumValue = value;
            }
        }

        /// <summary>
        /// Paralell limit
        /// Minimum value can be 1
        /// Maximum value can be CPU count
        /// </summary>
        public int ParallelLimit
        {
            get
            {
                var limit = new[] 
                {
                    MaximumValue,
                    configParallelLimit,
                    userParallelLimit
                }
                .Where(value => value > 0)
                .Min();

                return limit;
            }
        }

        public ParallelOptions ParallelOptions(double multiplier = 1)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = (int)Math.Round(ParallelLimit * multiplier)
            };

            return parallelOptions;
        }

        private int configParallelLimit = 0;

        private int userParallelLimit = 0;

        /// <summary>
        /// Default is number of the CPU Cores 
        /// then API limit
        /// then User limit
        /// </summary>
        /// <param name="siteConfig"></param>
        /// <param name="contextAccessor"></param>
        public ParallelService(SiteConfig siteConfig, IHttpContextAccessor contextAccessor)
        {
            if (siteConfig.Parallel.ConcurrentTasksLimit > 0)
            {
                configParallelLimit = siteConfig.Parallel.ConcurrentTasksLimit;
            }

            // At Startup contextAccessor not present
            if (contextAccessor?.HttpContext?.Request?.Headers != null && 
                contextAccessor.HttpContext.Request.Headers.ContainsKey(SDK.Net.Constants.ApiParallelLimitHeader))
            {
                int.TryParse(contextAccessor.HttpContext.Request.Headers[SDK.Net.Constants.ApiParallelLimitHeader], out userParallelLimit);
            }
        }
    }
}
