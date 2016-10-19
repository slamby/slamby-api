using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Cerebellum;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;

namespace Slamby.API.Helpers
{
    [TransientDependency]
    public class TagsHandler
    {
        readonly SiteConfig siteConfig;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;

        public IGlobalStoreManager GlobalStore { get; set; }

        public TagsHandler(SiteConfig siteConfig, ProcessHandler processHandler, IQueryFactory queryFactory, IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.siteConfig = siteConfig;
        }

        public void ExportWords(string processId, string dataSetName, List<string> tagIdList, List<int> nGramList, CancellationToken token, string hostUrl)
        {
            try
            {
                var globalStoreDataSet = GlobalStore.DataSets.Get(dataSetName);
                var dataSet = globalStoreDataSet.DataSet;
                var allDicCount = tagIdList.Count * nGramList.Count;
                /*ZIP time*/
                allDicCount += (allDicCount / 10);
                var counter = 0;

                foreach (var nGram in nGramList)
                {
                    var subsetCreator = new SubsetCreator(dataSet.Name, new List<string> { DocumentElastic.TextField }, dataSet.InterpretedFields.Select(DocumentQuery.MapDocumentObjectName).ToList(), nGram, queryFactory, globalStoreDataSet.AttachmentFields);

                    var tempDirectoryPath = string.Format("{0}/{1}/{2}", siteConfig.Directory.Temp, processId, nGram);
                    System.IO.Directory.CreateDirectory(tempDirectoryPath);

                    foreach (var tagId in tagIdList)
                    {
                        if (token.IsCancellationRequested)
                        {
                            processHandler.Cancelled(processId);
                            return;
                        }
                        //var filePath = $"{dictionariesPath}/{DictionaryProtoBuf.GetFileName(tagId)}";
                        //var dicProtoBuf = BaseProtoBuf.DeSerialize<DictionaryProtoBuf>(filePath);

                        var subset = subsetCreator.CreateByTag(tagId, dataSet.TagField);

                        var csvPath = $"{tempDirectoryPath}/{tagId}.csv";
                        CsvHelper.CreateCsv(csvPath, subset.WordsWithOccurences.Select(d => new List<string> { d.Key, d.Value.Tag.ToString(), d.Value.Corpus.ToString() }).ToList());

                        processHandler.Changed(processId, Math.Round(++counter / (double)allDicCount * 100, 2));
                    }
                }
                /*time to ZIP the results*/
                var zipFileName = string.Format("{0}.zip", processId);
                var dirToZipPath = string.Format("{0}/{1}", siteConfig.Directory.Temp, processId);
                var resultZipPath = string.Format("{0}/{1}.zip", siteConfig.Directory.User, processId);
                ZipHelper.CompressFolder(dirToZipPath, resultZipPath);

                var zipUrl = string.Format("{0}{1}/{2}", hostUrl, Common.Constants.FilesPath, zipFileName);

                processHandler.Finished(processId,
                    string.Format("{0}\n{1}",
                        string.Format(TagResources.SuccessfullyExportedWordsFrom_0_TagsOfDataset_1, tagIdList.Count, dataSet.Name),
                        string.Format(TagResources.ExportFileCanBeDownloadFromHere_0, zipUrl)));
            }
            catch (Exception ex)
            {
                processHandler.Interrupted(processId, ex);
            }
        }
    }
}
