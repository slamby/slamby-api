using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Slamby.API.Models;
using Slamby.API.Services.Interfaces;
using Slamby.Common;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.API.Resources;
using Slamby.Common.Config;

namespace Slamby.API.Services
{
    [SingletonDependency]
    public class DataSetService
    {
        readonly ServiceQuery serviceQuery;
        readonly IQueryFactory queryFactory;
        readonly IndexQuery indexQuery;
        readonly ElasticClientFactory clientFactory;
        readonly ILogger logger;
        readonly SiteConfig siteConfig;

        public IGlobalStoreManager GlobalStore { get; set; }

        public DataSetService(ServiceQuery serviceQuery, IQueryFactory queryFactory, 
            IndexQuery indexQuery, ElasticClientFactory clientFactory,
            ILoggerFactory loggerFactory, IGlobalStoreManager globalStore, SiteConfig siteConfig)
        {
            GlobalStore = globalStore;
            this.clientFactory = clientFactory;
            this.indexQuery = indexQuery;
            this.queryFactory = queryFactory;
            this.serviceQuery = serviceQuery;
            this.siteConfig = siteConfig;

            this.logger = loggerFactory.CreateLogger<DataSetService>();
        }

        internal Dictionary<string, DataSet> GetAllDataSet()
        {
            var resultDic = new Dictionary<string, DataSet>();
            var aliases = indexQuery.GetAliases();

            var properties = indexQuery.GetProperties(null);
            foreach (var aliasDefinition in aliases)
            {
                var name = aliasDefinition.Value.Select(a => a.Name).FirstOrDefault();
                var indexName = aliasDefinition.Key;

                if (!properties.ContainsKey(indexName)) continue;

                var dataSet = Convert(name ?? indexName, properties[indexName], 0);
                resultDic.Add(indexName, dataSet);
            }
            return resultDic;
        }

        

        public bool HasServiceReference(string name)
        {
            var dataSet = GlobalStore.DataSets.Get(name);
            return serviceQuery.GetSettingsByDataSet<ClassifierSettingsElastic>(dataSet.IndexName).Any() ||
                   serviceQuery.GetSettingsByDataSet<PrcSettingsElastic>(dataSet.IndexName).Any() ||
                   serviceQuery.GetSettingsByDataSet<SearchSettingsWrapperElastic>(dataSet.IndexName).Any();
        }

        public bool IsExists(string name)
        {
            return indexQuery.IsExists(name);
        }

        public IEnumerable<DataSet> Get()
        {
            var dataSets = new List<DataSet>();
            var cats = indexQuery.GetCats();
            var aliases = indexQuery.GetAliases();

            var properties = indexQuery.GetProperties(null);
            var countsDic = queryFactory.GetDocumentQuery().CountAll(aliases.Keys.ToList());

            foreach (var cat in cats)
            {
                if (!properties.ContainsKey(cat.Key)) continue;

                var aliasName = aliases[cat.Key].Select(s => s.Name).FirstOrDefault() ?? cat.Key;
                //if the dataset is busy then just skip it
                if (GlobalStore.DataSets.IsBusy(aliasName)) continue;
                var dataSet = Convert(aliasName, properties[cat.Key], (int)countsDic[cat.Key]);

                dataSets.Add(dataSet);
            }

            return dataSets;
        }

        public DataSet Get(string name)
        {
            var props = queryFactory.GetIndexQuery(name).GetProperties(name);
            var dataSetIndexName = GlobalStore.DataSets.Get(name).IndexName;
            if (!props.ContainsKey(name))
            {
                // TODO: Invalid DataSet creation at CreateIndex
            }

            var dataSet = Convert(name, props[dataSetIndexName], (int)queryFactory.GetDocumentQuery(name).CountAll());
            return dataSet;
        }

        private DataSet Convert(string name, PropertiesElastic props, int documentsCount)
        {
            return new DataSet
            {
                Name = name,
                NGramCount = props.NGramCount,
                IdField = props.IdField,
                InterpretedFields = props.InterPretedFields,
                TagField = props.TagField,
                SampleDocument = props.SampleDocument,
                Schema = props.Schema,
                Statistics = new DataSetStats
                {
                    DocumentsCount = documentsCount
                }
            };
        }

        public string Create(DataSet dataSet, bool withSchema)
        {
            try
            {
                GlobalStore.DataSets.AddBusy(dataSet.Name);
                var indexName = GenerateIndexName();

                if (!withSchema)
                {
                    indexQuery.CreateIndex(
                        dataSet.Name,
                        indexName,
                        dataSet.NGramCount,
                        dataSet.SampleDocument,
                        dataSet.IdField,
                        dataSet.InterpretedFields,
                        dataSet.TagField,
                        siteConfig.AvailabilityConfig.ClusterSize);
                }
                else
                {
                    indexQuery.CreateIndexWithSchema(
                    dataSet.Name,
                    indexName,
                    dataSet.NGramCount,
                    dataSet.Schema,
                    dataSet.IdField,
                    dataSet.InterpretedFields,
                    dataSet.TagField,
                    siteConfig.AvailabilityConfig.ClusterSize);
                }
                return indexName;
            }
            finally
            {
                GlobalStore.DataSets.RemoveBusy(dataSet.Name);
            }
        }

        private static string GenerateIndexName()
        {
            return Guid.NewGuid().ToString();
        }

        public void Update(string name, string newName)
        {
            try
            {
                GlobalStore.DataSets.AddBusy(name);
                GlobalStore.DataSets.AddBusy(newName);
                var dataSet = GlobalStore.DataSets.Get(name);
                if (dataSet == null)
                {
                    throw new InvalidOperationException();
                }

                // Apply the new alias first to check it is valid
                indexQuery.CreateAlias(dataSet.IndexName, newName);
                if (!string.IsNullOrEmpty(dataSet.AliasName) &&
                    indexQuery.IsAliasExist(dataSet.AliasName))
                {
                    indexQuery.RemoveAlias(dataSet.IndexName, dataSet.AliasName);
                }

                GlobalStore.DataSets.Rename(name, newName);
            }
            finally
            {
                GlobalStore.DataSets.RemoveBusy(name);
                GlobalStore.DataSets.RemoveBusy(newName);
            }
        }

        public void Delete(string name)
        {
            try
            {
                GlobalStore.DataSets.AddBusy(name);
                var dataSet = GlobalStore.DataSets.Get(name);
                if (dataSet == null)
                {
                    throw new InvalidOperationException();
                }

                if (!string.IsNullOrEmpty(dataSet.AliasName))
                {
                    indexQuery.RemoveAlias(dataSet.IndexName, dataSet.AliasName);
                }

                indexQuery.Delete(dataSet.IndexName);
                GlobalStore.DataSets.Remove(name);
            }
            finally
            {
                GlobalStore.DataSets.RemoveBusy(name);
            }
        }

        public Dictionary<string, string> GetDataSetIndexAlias()
        {
            var result = new Dictionary<string, string>();
            var aliases = indexQuery.GetAliases();

            foreach (var aliasDefinition in indexQuery.GetAliases())
            {
                var name = aliasDefinition.Value.Select(a => a.Name).FirstOrDefault();
                var indexName = aliasDefinition.Key;

                result.Add(indexName, name);
            }

            return result;
        }

        public static List<string> ValidateDataSetName(string name)
        {
            var result = new List<string>();
            if (!new System.Text.RegularExpressions.Regex(SDK.Net.Constants.ValidationCommonRegex).IsMatch(name))
                result.Add(string.Format(DataSetResources.The_Dataset_name_MustMatchRegex_0, SDK.Net.Constants.ValidationCommonRegex));
            if (name.Length < SDK.Net.Constants.ValidationCommonMinimumLength || name.Length > SDK.Net.Constants.ValidationCommonMaximumLength)
                result.Add(string.Format(DataSetResources.The_Dataset_name_MustBeMin_0_Max_1,
                    SDK.Net.Constants.ValidationCommonMinimumLength,
                    SDK.Net.Constants.ValidationCommonMaximumLength));
            return result;
        }

        public void ThrowIfDataSetIsBusy(string name)
        {
            GlobalStore.DataSets.ThrowIfBusy(name);
        }
    }
}
