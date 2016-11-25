using System;
using Microsoft.Extensions.Logging;
using Slamby.Common.DI;
using StackExchange.Redis;
using Slamby.Common.Config;

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
            centralRedis = ((bool)siteConfig.Stats?.Enabled) ? ConnectionMultiplexer.Connect(siteConfig.Stats.Redis.Configuration) : null;

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
