using System.Collections.Generic;
using Slamby.Common.DI;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreServiceAliasRepository))]
    public class GlobalStoreServiceAliasRepository : IGlobalStoreServiceAliasRepository
    {
        /// <summary>
        /// Key: Alias; Value: ServiceId
        /// </summary>
        public IDictionary<string, string> ServiceAliases { get; } = new Dictionary<string, string>();

        public string Get(string alias)
        {
            if (string.IsNullOrEmpty(alias))
            {
                return string.Empty;
            }

            if (IsExist(alias))
            {
                return ServiceAliases[alias];
            }

            return string.Empty;
        }

        public void Set(string alias, string id)
        {
            if (string.IsNullOrEmpty(alias) ||
               string.IsNullOrEmpty(id))
            {
                return;
            }

            if (ServiceAliases.ContainsKey(alias))
            {
                ServiceAliases[alias] = id;
            }
            else
            {
                ServiceAliases.Add(alias, id);
            }
        }

        public void Remove(string alias)
        {
            if (string.IsNullOrEmpty(alias))
            {
                return;
            }

            if (ServiceAliases.ContainsKey(alias))
            {
                ServiceAliases.Remove(alias);
            }
        }

        public bool IsExist(string alias)
        {
            return ServiceAliases.ContainsKey(alias);
        }
    }
}