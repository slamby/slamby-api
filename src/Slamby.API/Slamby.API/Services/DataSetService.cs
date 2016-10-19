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

        public IGlobalStoreManager GlobalStore { get; set; }

        public DataSetService(ServiceQuery serviceQuery, IQueryFactory queryFactory, 
            IndexQuery indexQuery, ElasticClientFactory clientFactory,
            ILoggerFactory loggerFactory, IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.clientFactory = clientFactory;
            this.indexQuery = indexQuery;
            this.queryFactory = queryFactory;
            this.serviceQuery = serviceQuery;

            this.logger = loggerFactory.CreateLogger<DataSetService>();
        }

        internal void LoadGlobalStore()
        {
            var aliases = indexQuery.GetAliases();

            var properties = indexQuery.GetProperties(null);
            var countsDic = queryFactory.GetDocumentQuery().CountAll(aliases.Keys.ToList());

            foreach (var aliasDefinition in aliases)
            {
                var name = aliasDefinition.Value.Select(a => a.Name).FirstOrDefault();
                var indexName = aliasDefinition.Key;

                if (!properties.ContainsKey(indexName)) continue;

                var dataSet = Convert(name ?? indexName, properties[indexName], (int)countsDic[indexName]);
                AddGlobalStoreInternal(name, indexName, dataSet);
            }
        }

        private void AddGlobalStoreInternal(string name, string indexName, DataSet dataSet)
        {
            var paths = new List<string>();
            var attachments = new List<string>();
            var tagIsArray = false;
            var tagIsInteger = false;

            if (dataSet.SampleDocument != null)
            {
                var pathTokens = DocumentHelper.GetAllPathTokens(dataSet.SampleDocument);
                paths = pathTokens.Keys.ToList();

                if (!pathTokens.ContainsKey(dataSet.TagField))
                {
                    throw new InvalidOperationException($"DataSet Name: ´{name}´, Index: ´{indexName}´. SampleDocument does not contain TagField ´{dataSet.TagField}´");
                }

                var tagToken = pathTokens[dataSet.TagField];
                tagIsArray = tagToken.Type == JTokenType.Array;

                if (tagIsArray)
                {
                    tagIsInteger = JTokenHelper.GetUnderlyingToken(tagToken).Type == JTokenType.Integer;
                }
                else
                {
                    tagIsInteger = tagToken.Type == JTokenType.Integer;
                }
            }
            else if (dataSet.Schema != null)
            {
                var pathDictionary = SchemaHelper.GetPaths(dataSet.Schema);

                paths = pathDictionary.Keys.ToList();
                attachments = pathDictionary
                    .Where(kv => kv.Value.Item1 == SchemaHelper.Types.Attachment)
                    .Select(kv => kv.Key)
                    .ToList();

                if (!pathDictionary.ContainsKey(dataSet.TagField))
                {
                    throw new InvalidOperationException($"DataSet Name: ´{name}´, Index: ´{indexName}´. Schema does not contain TagField ´{dataSet.TagField}´");
                }

                var tagTuple = pathDictionary[dataSet.TagField];
                tagIsArray = SchemaHelper.IsArray(tagTuple.Item1);
                if (tagIsArray)
                {
                    tagIsInteger = SchemaHelper.IsInteger(tagTuple.Item2);
                }
                else
                {
                    tagIsInteger = SchemaHelper.IsInteger(tagTuple.Item1);
                }
            }
            else
            {
                throw new InvalidOperationException("DataSet has no SampleDocument nor Schema property");
            }

            GlobalStore.DataSets.Add(name ?? indexName, new GlobalStoreDataSet(name, indexName, dataSet, paths, tagIsArray, tagIsInteger, attachments));
        }

        public bool HasServiceReference(string name)
        {
            var dataSet = GlobalStore.DataSets.Get(name);
            return serviceQuery.GetSettingsByDataSet<ClassifierSettingsElastic>(dataSet.IndexName).Any() ||
                   serviceQuery.GetSettingsByDataSet<PrcSettingsElastic>(dataSet.IndexName).Any();
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

        public void Create(DataSet dataSet, bool withSchema)
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
                        dataSet.TagField);
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
                    dataSet.TagField);
                }

                AddGlobalStoreInternal(dataSet.Name, indexName, dataSet);
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
