using System;
using Microsoft.Extensions.Logging;
using Slamby.Common.DI;
using StackExchange.Redis;
using Slamby.Common.Config;
using System.Net;
using Slamby.API.Helpers;

namespace Slamby.API.Services
{
    [SingletonDependency]
    public class ThrottleService
    {
        private ILogger logger;
        readonly ConnectionMultiplexer redis;
        readonly ConnectionMultiplexer centralRedis;

        public ThrottleService(ConnectionMultiplexer redis, SiteConfig siteConfig, ILoggerFactory loggerFactory)
        {
            if (siteConfig.Stats != null && siteConfig.Stats.Enabled)
            {
                var options = RedisDnsHelper.CorrectOption(ConfigurationOptions.Parse(siteConfig.Stats.Redis.Configuration));
                centralRedis = ConnectionMultiplexer.Connect(options);
            }

            this.redis = redis;
            this.logger = loggerFactory.CreateLogger<ThrottleService>();
        }
        
        public void SaveRequest(string instanceId, string endpoint)
        {
            if (!redis.IsConnected && !centralRedis.IsConnected)
            {
                return;
            }
            var date = DateTime.UtcNow.ToString("yyyy-MM");

            IDatabase db = null;

            if (centralRedis.IsConnected)
            {
                db = centralRedis.GetDatabase();
            } else if (redis.IsConnected)
            {
                db = redis.GetDatabase();
            }
            else
            {
                return;
            }
            db.SortedSetIncrement(instanceId, $"{date}:{endpoint}", 1, CommandFlags.FireAndForget);
        }
    }
}
