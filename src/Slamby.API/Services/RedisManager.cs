using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Slamby.Common.DI;
using StackExchange.Redis;

namespace Slamby.Common.Services
{
    [TransientDependency]
    public class RedisManager
    {
        // Increase writeBuffer (StackExchange BUG??) in order to increase BatchSize
        public int BatchSize { get; set; } = 100;

        public int DbNo { get; set; } = -1;

        public CommandFlags CommandFlags { get; set; } = CommandFlags.HighPriority;

        private IDatabase Database => Connection.GetDatabase(DbNo);

        private ConnectionMultiplexer Connection { get; set; }

        private ConfigurationOptions Options { get; set; }

        public RedisManager(ConnectionMultiplexer connection)
        {
            Connection = connection;
        }

        public void DeleteKeys(string pattern)
        {
            var keysToDelete = GetKeys(pattern)
                .OrderBy(o => o.ToString())
                .ToList();

            foreach (var batchKeys in keysToDelete.Batch(BatchSize))
            {
                Database.KeyDelete(batchKeys.ToArray());
            }
        }

        public void DeleteKey(string key)
        {
            Database.KeyDelete(key);
        }

        public RedisKey[] GetKeys(string pattern)
        {
            var keys = new List<RedisKey>();
            foreach (var endpoint in Connection.GetEndPoints(true))
            {
                var server = Connection.GetServer(endpoint);
                keys.AddRange(server.Keys(DbNo, pattern));
            }
            return keys.ToArray();
        }

        public void HashSet(string key, IEnumerable<KeyValuePair<RedisValue, RedisValue>> values)
        {
            foreach (var batchItems in values.Batch(BatchSize))
            {
                Database.HashSet(
                    key,
                    batchItems.Select(s => new HashEntry(s.Key, s.Value)).ToArray(),
                    flags: CommandFlags);
            }
        }

        public void SortedSetAdd(string key, IEnumerable<KeyValuePair<string, double>> values)
        {
            var database = Database;
            foreach (var batchItems in values.Batch(BatchSize))
            {
                database.SortedSetAdd(
                    key,
                    batchItems.Select(s => new SortedSetEntry(s.Key, s.Value)).ToArray(),
                    flags: CommandFlags);
            }
        }

        public void SortedSetAdd(string key, string name, double score)
        {
            Database.SortedSetAdd(
                key,
                new SortedSetEntry[] { new SortedSetEntry(name, score) },
                flags: CommandFlags);
        }

        public void SortedSetRemove(string key, string name)
        {
            Database.SortedSetRemove(key, name, flags: CommandFlags);
        }

        public long SortedSetRemoveByRank(string key, long start, long stop)
        {
            return Database.SortedSetRemoveRangeByRank(key, start, stop, flags: CommandFlags);
        }

        public IEnumerable<SortedSetEntry> SortedSetRangeByRank(string key, long start = 0, long stop = -1 , Order order = Order.Descending)
        {
            return Database.SortedSetRangeByRankWithScores(key, start, stop, order, flags: CommandFlags);
        }

        public void HashSet(string key, RedisValue name, RedisValue value)
        {
            Database.HashSet(
                key,
                new HashEntry[] { new HashEntry(name, value) },
                flags: CommandFlags);
        }

        public IEnumerable<HashEntry> HashGet(string key)
        {
            return Database.HashGetAll(key);
        }

        public void HashDelete(string key, RedisValue name)
        {
            Database.HashDelete(key, name);
        }
    }
}
