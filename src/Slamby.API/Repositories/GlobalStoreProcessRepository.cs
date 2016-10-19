using System.Collections.Generic;
using Slamby.API.Models;
using Slamby.API.Repositories.Interfaces;
using Slamby.Common.DI;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreProcessRepository))]
    public class GlobalStoreProcessRepository : IGlobalStoreProcessRepository
    {
        private Dictionary<string, GlobalStoreProcess> ProcessesDictionary { get; set; } = new Dictionary<string, GlobalStoreProcess>();

        public void Add(string name, GlobalStoreProcess process)
        {
            ProcessesDictionary.Add(name, process);
        }

        public GlobalStoreProcess Get(string id)
        {
            if (string.IsNullOrEmpty(id) || !ProcessesDictionary.ContainsKey(id))
            {
                return null;
            }

            return ProcessesDictionary[id];
        }

        public bool IsExist(string id)
        {
            return ProcessesDictionary.ContainsKey(id);
        }

        public void Remove(string id)
        {
            ProcessesDictionary.Remove(id);
        }
    }
}