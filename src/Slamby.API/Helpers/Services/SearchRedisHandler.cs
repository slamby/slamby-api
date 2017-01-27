using Slamby.Common.DI;
using Slamby.Common.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class SearchRedisHandler
    {
        const int RedisDb = 2;

        RedisManager redisManager { get; }

        public SearchRedisHandler(RedisManager redisManager)
        {
            redisManager.DbNo = RedisDb;
            this.redisManager = redisManager;
            this.redisManager.CommandFlags = CommandFlags.FireAndForget;
        }

        public void SaveSearch(string serviceId, string text)
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            redisManager.SortedSetIncrement($"{serviceId}:{date}", text);
        }
    }
}
