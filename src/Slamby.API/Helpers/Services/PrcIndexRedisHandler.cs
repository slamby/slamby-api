using System.Collections.Generic;
using System.Linq;
using Slamby.Common.DI;
using Slamby.Common.Services;
using StackExchange.Redis;

namespace Slamby.API.Helpers.Services
{
    public class PrcIndexRedisKey
    {
        public string ServiceId { get; set; }
        public string TagId { get; set; }
        public string DocumentId { get; set; }

        public PrcIndexRedisKey(string serviceId, string tagId, string documentId)
        {
            DocumentId = documentId;
            TagId = tagId;
            ServiceId = serviceId;
        }

        public static string RedisKey(string serviceId, string tagId, string documentId) => $"service:{serviceId}:tag:{tagId}:document:{documentId}:prc";
        public static string ServiceDeleteKey(string serviceId) => $"service:{serviceId}:*:prc";

        public static implicit operator string(PrcIndexRedisKey key)
        {
            return RedisKey(key.ServiceId, key.TagId, key.DocumentId);
        }
    }

    [TransientDependency]
    public class PrcIndexRedisHandler
    {
        const int RedisDb = 1;

        const int MaxSortedSetItems = 100;

        RedisManager redisManager { get; }

        public PrcIndexRedisHandler(RedisManager redisManager)
        {
            redisManager.DbNo = RedisDb;
            this.redisManager = redisManager;
        }

        public void Clean(string deleteKey)
        {
            redisManager.DeleteKeys(deleteKey);
        }

        public void DeleteKey(string redisKey)
        {
            redisManager.DeleteKeys(redisKey);
        }

        public void ReplaceDocuments(string redisKey, IEnumerable<KeyValuePair<string, double>> documents)
        {
            DeleteKey(redisKey);
            AddDocuments(redisKey, documents);
        }

        public void AddDocuments(string redisKey, IEnumerable<KeyValuePair<string, double>> documents)
        {
            var topScoreDocuments = documents
                .OrderByDescending(o => o.Value)
                .Take(MaxSortedSetItems);

            redisManager.SortedSetAdd(redisKey, topScoreDocuments);
        }

        public void AddDocument(string redisKey, string id, double score)
        {
            redisManager.SortedSetAdd(redisKey, id, score);
        }

        public IEnumerable<SortedSetEntry> GetDocuments(string redisKey)
        {
            return redisManager.SortedSetRangeByRank(redisKey);
        }

        public Dictionary<string, double> GetTopNDocuments(string redisKey, long count = -1)
        {
            return redisManager
                .SortedSetRangeByRank(redisKey, 0, count)
                .ToDictionary(
                    k => k.Element.ToString(), 
                    v => v.Score);
        }

        public void TrimDocuments(string redisKey)
        {
            redisManager.SortedSetRemoveByRank(redisKey, 0, -1 * (MaxSortedSetItems + 1));
        }

        public void RemoveDocument(string redisKey, string documentIdToRemove)
        {
            redisManager.SortedSetRemove(redisKey, documentIdToRemove);
        }
    }
}
