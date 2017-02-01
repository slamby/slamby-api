using Slamby.API.Models;
using Slamby.API.Repositories.Interfaces;
using Slamby.Common.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreSearchRepository))]
    public class GlobalStoreSearchRepository : IGlobalStoreSearchRepository
    {
        private Dictionary<string, GlobalStoreSearch> SearchDictionary { get; set; } = new Dictionary<string, GlobalStoreSearch>();

        public bool IsExist(string id)
        {
            return SearchDictionary.ContainsKey(id);
        }

        public GlobalStoreSearch Get(string id)
        {
            if (SearchDictionary.ContainsKey(id))
            {
                return SearchDictionary[id];
            }

            return null;
        }

        public void Add(string id, GlobalStoreSearch classifier)
        {
            SearchDictionary.Add(id, classifier);
        }

        public void Remove(string id)
        {
            SearchDictionary.Remove(id);
        }
    }
}
