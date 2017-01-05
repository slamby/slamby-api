using Slamby.API.Services;
using Slamby.Common.DI;
using Slamby.Common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class StatisticsRedisHandler
    {
        const int RedisDb = 0;
        RedisManager redisManager { get; }
        string instanceId;

        public StatisticsRedisHandler(RedisManager redisManager, ILicenseManager licenseManager)
        {
            redisManager.DbNo = RedisDb;
            this.redisManager = redisManager;
            instanceId = licenseManager.InstanceId.ToString();

        }

        public int GetRequestCount(string prefix)
        {
            var entries = redisManager.SortedSetRangeByRank(instanceId);
            var actualMonthScore = 0.0;
            actualMonthScore = entries.Where(rv => rv.ToString().StartsWith(prefix)).Sum(rv => rv.Score);

            return Convert.ToInt32(actualMonthScore);
        }

        public int GetAllRequestCount()
        {
            var entries = redisManager.SortedSetRangeByRank(instanceId);
            var score = entries.Sum(rv => rv.Score);
            return Convert.ToInt32(score);
        }

        public Dictionary<string, Dictionary<string, int>> GetRequests(string prefix)
        {
            var resultDic = new Dictionary<string, Dictionary<string, int>>();
            var entries = redisManager.SortedSetRangeByRank(instanceId).Where(rv => rv.ToString().StartsWith(prefix));
            foreach (var entry in entries)
            {
                var splitted = entry.Element.ToString().Split(':');
                if (!resultDic.ContainsKey(splitted[0])) resultDic.Add(splitted[0], new Dictionary<string, int>());
                resultDic[splitted[0]].Add(splitted[1], (int)entry.Score);
            }
            return resultDic;
        }
    }
}
