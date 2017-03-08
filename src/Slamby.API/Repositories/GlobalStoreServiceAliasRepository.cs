using System.Collections.Generic;
using Slamby.Common.DI;
using Slamby.Common.Exceptions;
using Slamby.API.Resources;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreServiceAliasRepository))]
    public class GlobalStoreServiceAliasRepository : IGlobalStoreServiceAliasRepository
    {
        /// <summary>
        /// Key: Alias; Value: ServiceId
        /// </summary>
        public IDictionary<string, string> ServiceAliases { get; } = new Dictionary<string, string>();

        private List<string> BusyServices { get; set; } = new List<string>();

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

        public void AddBusy(string idOrAlias)
        {
            var id = IsExist(idOrAlias) ? Get(idOrAlias) : idOrAlias;
            ThrowIfBusy(id, idOrAlias);
            BusyServices.Add(id);
        }
        public void RemoveBusy(string idOrAlias)
        {
            var id = IsExist(idOrAlias) ? Get(idOrAlias) : idOrAlias;
            BusyServices.Remove(id);
        }
        public void ThrowIfBusy(string id, string original)
        {
            if (IsBusy(id)) throw new SlambyException(string.Format(ServiceResources.Operation_is_not_allowed_the_Service_0_is_busy, original));
        }

        public bool IsBusy(string id)
        {
            return BusyServices.Contains(id);
        }
    }
}