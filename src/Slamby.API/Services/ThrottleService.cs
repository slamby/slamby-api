using System;
using Microsoft.Extensions.Logging;
using Slamby.Common.DI;
using StackExchange.Redis;
using Slamby.Common.Config;
using System.Net;
using Slamby.API.Helpers;
using System.Collections.Generic;

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
                var options = ConfigurationOptions.Parse(siteConfig.Stats.Redis.Configuration);
                options.CommandMap = CommandMap.Create(siteConfig.Stats.Redis.CommandMap);

                options = RedisDnsHelper.CorrectOption(options);
                if (options != null) centralRedis = ConnectionMultiplexer.Connect(options);
            }
            this.redis = redis;
            this.logger = loggerFactory.CreateLogger<ThrottleService>();
        }
        
        public void SaveRequest(string instanceId, string endpoint)
        {
            if (!redis.IsConnected && (centralRedis == null || !centralRedis.IsConnected))
            {
                return;
            }
            var date = DateTime.UtcNow.ToString("yyyy-MM");

            IDatabase db = null;

            if (centralRedis != null && centralRedis.IsConnected)
            {
                db = centralRedis.GetDatabase();
                db.SortedSetIncrement(instanceId, $"{date}:{endpoint}", 1, CommandFlags.FireAndForget);
            }

            if (redis.IsConnected)
            {
                db = redis.GetDatabase();
                db.SortedSetIncrement(instanceId, $"{date}:{endpoint}", 1, CommandFlags.FireAndForget);
            }
        }
    }
}
