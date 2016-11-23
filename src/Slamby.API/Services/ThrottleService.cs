using System;
using Microsoft.Extensions.Logging;
using Slamby.Common.DI;
using StackExchange.Redis;

namespace Slamby.API.Services
{
    [SingletonDependency]
    public class ThrottleService
    {
        private ILogger logger;
        readonly ConnectionMultiplexer redis;

        public ThrottleService(ConnectionMultiplexer redis, ILoggerFactory loggerFactory)
        {
            this.redis = redis;
            this.logger = loggerFactory.CreateLogger<ThrottleService>();
        }
        
        public void SaveRequest(string instanceId, string endpoint)
        {
            if (!redis.IsConnected)
            {
                logger.LogWarning("Redis connection is offline");
                return;
            }

            var date = DateTime.UtcNow.ToString("yyyy-MM");
            var db = redis.GetDatabase();

            db.SortedSetIncrement(instanceId, $"{date}:{endpoint}", 1, CommandFlags.FireAndForget);
        }
    }
}
