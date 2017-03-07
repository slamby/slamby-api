using Newtonsoft.Json.Linq;
using Slamby.API.Models;
using Slamby.API.Services.Interfaces;
using Slamby.Common;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.SDK.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    [TransientDependency]
    public class LoadService
    {
        readonly IGlobalStoreManager globalStore;
        readonly SiteConfig siteConfig;
        readonly DataSetService dataSetService;

        public LoadService(IGlobalStoreManager globalStore, SiteConfig siteConfig, DataSetService dataSetService)
        {
            this.globalStore = globalStore;
            this.siteConfig = siteConfig;
            this.dataSetService = dataSetService;
        }

        public void ReloadDataSets()
        {
            var dataSetsDic = dataSetService.GetAllDataSet();
            foreach (var dataSetKvp in dataSetsDic)
            {
                if(globalStore.DataSets.IsExist(dataSetKvp.Value.Name)) globalStore.DataSets.Remove(dataSetKvp.Value.Name);
                AddDataSetGlobalStore(dataSetKvp.Value.Name, dataSetKvp.Key, dataSetKvp.Value);
            }
        }

        internal void AddDataSetGlobalStore(string name, string indexName, DataSet dataSet)
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

            globalStore.DataSets.Add(name, new GlobalStoreDataSet(name, indexName, dataSet, paths, tagIsArray, tagIsInteger, attachments));
        }
    }
}
