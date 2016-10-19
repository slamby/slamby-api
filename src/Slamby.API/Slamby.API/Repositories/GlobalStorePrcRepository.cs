using System.Collections.Generic;
using Slamby.API.Models;
using Slamby.API.Repositories.Interfaces;
using Slamby.Common.DI;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStorePrcRepository))]
    public class GlobalStorePrcRepository : IGlobalStorePrcRepository
    {
        private Dictionary<string, GlobalStorePrc> PrcDictionary { get; set; } = new Dictionary<string, GlobalStorePrc>();

        public bool IsExist(string id)
        {
            return PrcDictionary.ContainsKey(id);
        }

        public GlobalStorePrc Get(string id)
        {
            if (PrcDictionary.ContainsKey(id))
            {
                return PrcDictionary[id];
            }

            return null;
        }

        public void Add(string id, GlobalStorePrc prc)
        {
            PrcDictionary.Add(id, prc);
        }

        public void Remove(string id)
        {
            PrcDictionary.Remove(id);
        }
    }
}