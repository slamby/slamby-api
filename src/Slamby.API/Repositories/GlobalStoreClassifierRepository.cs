using System.Collections.Generic;
using Slamby.API.Models;
using Slamby.API.Repositories.Interfaces;
using Slamby.Common.DI;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreClassifierRepository))]
    public class GlobalStoreClassifierRepository : IGlobalStoreClassifierRepository
    {
        private Dictionary<string, GlobalStoreClassifier> ClassifierDictionary { get; set; } = new Dictionary<string, GlobalStoreClassifier>();

        public bool IsExist(string id)
        {
            return !string.IsNullOrEmpty(id) && ClassifierDictionary.ContainsKey(id);
        }

        public GlobalStoreClassifier Get(string id)
        {
            if (!string.IsNullOrEmpty(id) && ClassifierDictionary.ContainsKey(id))
            {
                return ClassifierDictionary[id];
            }

            return null;
        }

        public void Add(string id, GlobalStoreClassifier classifier)
        {
            ClassifierDictionary.Add(id, classifier);
        }

        public void Remove(string id)
        {
            ClassifierDictionary.Remove(id);
        }
    }
}
