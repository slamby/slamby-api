using System.Collections.Generic;
using System.Linq;
using Slamby.API.Models;
using Slamby.API.Repositories.Interfaces;
using Slamby.Common.DI;
using Slamby.Common.Exceptions;
using Slamby.API.Resources;

namespace Slamby.API.Repositories
{
    [TransientDependency(ServiceType = typeof(IGlobalStoreDataSetRepository))]
    public class GlobalStoreDataSetRepository : IGlobalStoreDataSetRepository
    {
        private Dictionary<string, GlobalStoreDataSet> DataSetsDictionary { get; set; } = new Dictionary<string, GlobalStoreDataSet>();

        private List<string> BusyDataSets { get; set; } = new List<string>();

        /// <summary>
        /// Checks if the specified DataSet is exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsExist(string name)
        {
            return !string.IsNullOrEmpty(name) && DataSetsDictionary.ContainsKey(name);
        }

        /// <summary>
        /// Gets the specified DataSet
        /// </summary>
        /// <param name="name">DataSet Name or Index Nam</param>
        /// <returns></returns>
        public GlobalStoreDataSet Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return DataSetsDictionary
                .Where(d => string.Equals(d.Value.AliasName, name, System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(d.Value.IndexName, name, System.StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Value)
                .FirstOrDefault();
        }

        public void Add(string name, GlobalStoreDataSet dataSet)
        {
            DataSetsDictionary.Add(name, dataSet);
        }

        public void Rename(string name, string newName)
        {
            var temp = Get(name);
            Remove(name);

            temp.AliasName = newName;
            temp.DataSet.Name = newName;
            Add(newName, temp);
        }

        public void Remove(string name)
        {
            DataSetsDictionary.Remove(name);
        }

        public void AddBusy(string name)
        {
            ThrowIfBusy(name);
            BusyDataSets.Add(name);
        }
        public void RemoveBusy(string name)
        {
            BusyDataSets.Remove(name);
        }
        public void ThrowIfBusy(string name)
        {
            if (IsBusy(name)) throw new SlambyException(string.Format(DataSetResources.Operation_is_not_allowed_the_dataset_0_is_busy, name));
        }

        public bool IsBusy(string name)
        {
            return BusyDataSets.Contains(name);
        }

    }
}
