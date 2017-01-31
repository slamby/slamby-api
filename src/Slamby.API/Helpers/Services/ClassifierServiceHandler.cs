using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Slamby.API.Helpers.Services.Interfaces;
using Slamby.API.Models.Serializable;
using Slamby.API.Resources;
using Slamby.Cerebellum;
using Slamby.Cerebellum.Dictionary;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using Slamby.Common;
using System.Collections.Concurrent;
using Slamby.API.Models;
using Slamby.API.Services.Interfaces;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.SDK.Net.Models.Services;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class ClassifierServiceHandler : IServiceHandler<ClassifierSettingsElastic>
    {
        private readonly string _dictionaryRootPath;
        readonly SiteConfig siteConfig;
        readonly ServiceQuery serviceQuery;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;
        readonly ParallelService parallelService;
        readonly MachineResourceService machineResourceService;

        public string GetDirectoryPath(string serviceId)
        {
            return string.Format("{0}/{1}", _dictionaryRootPath, serviceId);
        }

        public IGlobalStoreManager GlobalStore { get; set; }

        public ClassifierServiceHandler(SiteConfig siteConfig, ServiceQuery serviceQuery, ProcessHandler processHandler, 
            IQueryFactory queryFactory, ParallelService parallelService, MachineResourceService machineResourceService, 
            IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
            this.parallelService = parallelService;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.serviceQuery = serviceQuery;
            this.siteConfig = siteConfig;
            this.machineResourceService = machineResourceService;
            _dictionaryRootPath = siteConfig.Directory.Classifier;
        }

        public void Prepare(string processId, ClassifierSettingsElastic settings, CancellationToken token)
        {
            var directoryPath = GetDirectoryPath(settings.ServiceId);
            try
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Busy;
                serviceQuery.Update(service.Id, service);

                IOHelper.SafeDeleteDictionary(directoryPath, true);

                var globalStoreDataSet = GlobalStore.DataSets.Get(settings.DataSetName);
                var dataSet = globalStoreDataSet.DataSet;
                var allDicCount = settings.NGramList.Count * settings.Tags.Count;
                var counter = 0;
                var lockObject = new object();

                Directory.CreateDirectory(directoryPath);

                foreach (var nGram in settings.NGramList)
                {
                    var subsetCreator = new SubsetCreator(dataSet.Name, new List<string> { DocumentElastic.TextField }, dataSet.InterpretedFields.Select(DocumentQuery.MapDocumentObjectName).ToList(), nGram, queryFactory, globalStoreDataSet.AttachmentFields);
                    var actualDirectory = string.Format("{0}/{1}", directoryPath, nGram);

                    Directory.CreateDirectory(actualDirectory);

                    Parallel.ForEach(settings.Tags, parallelService.ParallelOptions(), (tag, loopState) => {
                        token.ThrowIfCancellationRequested();

                        var subset = subsetCreator.CreateByTag(tag.Id, dataSet.TagField);
                        var dictionary = new TwisterAlgorithm(
                            subset, true, false, 
                            settings.CompressSettings.CompressCategoryOccurence,
                            settings.CompressSettings.CompressDataSetOccurence,
                            (LogicalOperatorEnum)settings.CompressSettings.CompressOperator).GetDictionary();
                        var dicProtoBuf = new DictionaryProtoBuf
                        {
                            Id = tag.Id,
                            Dictionary = dictionary,
                            NGram = nGram
                        };
                        
                        lock (lockObject)
                        {
                            dicProtoBuf.Serialize(string.Format("{0}/{1}", actualDirectory, dicProtoBuf.GetFileName()));
                            processHandler.Changed(processId, Math.Round(++counter / (double)allDicCount * 100, 2));
                        }
                    });
                }

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyPrepared_0_Service_1, ServiceTypeEnum.Classifier, service.Name));
                service.Status = (int)ServiceStatusEnum.Prepared;
                serviceQuery.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.New;
                serviceQuery.Update(service.Id, service);
                IOHelper.SafeDeleteDictionary(directoryPath, true);

                if (ex.InnerException != null && ex.InnerException is OperationCanceledException) {
                    processHandler.Cancelled(processId);
                } else
                {
                    processHandler.Interrupted(processId, ex);
                }
            }
        }

        public void Activate(string processId, ClassifierSettingsElastic settings, CancellationToken token)
        {
            try
            {
                GC.Collect();
                machineResourceService.UpdateResourcesManually();
                var freeMemInBytes = machineResourceService.Status.FreeMemory * 1024 * 1024;

                var dictionaryPaths = new List<string>();
                foreach (var nGram in settings.ActivatedNGramList)
                {
                    var directoryPath = string.Format("{0}/{1}/{2}", _dictionaryRootPath, settings.ServiceId, nGram);
                    var fileList = IOHelper.GetFilesInFolder(directoryPath, DictionaryProtoBuf.GetExtension())
                        .Where(file => settings.ActivatedTagIdList.Contains(Path.GetFileNameWithoutExtension(file)));
                    dictionaryPaths.AddRange(fileList);
                }

                var sizeInBytes = dictionaryPaths.Sum(f => new FileInfo(f).Length);
                if (freeMemInBytes > 0 && freeMemInBytes < sizeInBytes * Constants.DictionaryInMemoryMultiplier)
                {
                    throw new Common.Exceptions.OutOfResourceException(ServiceResources.NotEnoughResourceToActivateService);
                }

                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Busy;
                serviceQuery.Update(service.Id, service);

                var lockObject = new object();
                var counter = 0;
                var allCount = dictionaryPaths.Count;

                var deserializedDics = new ConcurrentBag<DictionaryProtoBuf>();
                Parallel.ForEach(dictionaryPaths, parallelService.ParallelOptions(), (path, loopState) => {
                    token.ThrowIfCancellationRequested();
                    deserializedDics.Add(BaseProtoBuf.DeSerialize<DictionaryProtoBuf>(path));
                    lock (lockObject)
                    {
                        if (++counter % 15 == 0) processHandler.Changed(processId, Math.Round(counter / (double)allCount * 100, 2));
                    }
                });

                var globalStoreClassifier = new GlobalStoreClassifier();

                if (deserializedDics.Any())
                {
                    var scorersDic = deserializedDics.GroupBy(d => d.Id).ToDictionary(d => d.Key, d => new Cerebellum.Scorer.PeSScorer(d.ToDictionary(di => di.NGram, di => di.Dictionary)));
                    globalStoreClassifier.ClassifierScorers = scorersDic;
                }

                var tagsDic = settings.Tags.ToDictionary(
                    t => t.Id,
                    t => new SDK.Net.Models.Tag
                    {
                        Id = t.Id,
                        Name = t.Name,
                        ParentId = t.ParentId()
                    }
                );

                var analyzeQuery = queryFactory.GetAnalyzeQuery(settings.DataSetName);

                var emphasizedTagsWords = new Dictionary<string, List<string>>();
                foreach (var tagId in settings.EmphasizedTagIdList)
                {
                    var tokens = analyzeQuery.Analyze(tagsDic[tagId].Name, 1).ToList();
                    emphasizedTagsWords.Add(tagId, tokens);
                }

                globalStoreClassifier.ClassifierEmphasizedTagIds =  emphasizedTagsWords;
                globalStoreClassifier.ClassifiersSettings = settings;
                globalStoreClassifier.ClassifiersTags = tagsDic;

                GlobalStore.ActivatedClassifiers.Add(settings.ServiceId, globalStoreClassifier);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyActivated_0_Service_1, ServiceTypeEnum.Classifier, service.Name));
                service.Status = (int)ServiceStatusEnum.Active;
                serviceQuery.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = serviceQuery.Get(settings.ServiceId);
                service.Status = (int)ServiceStatusEnum.Prepared;
                serviceQuery.Update(service.Id, service);
                if (GlobalStore.ActivatedClassifiers.IsExist(settings.ServiceId)) GlobalStore.ActivatedClassifiers.Remove(settings.ServiceId);
                if (ex.InnerException != null && ex.InnerException is OperationCanceledException)
                {
                    processHandler.Cancelled(processId);
                }
                else
                {
                    processHandler.Interrupted(processId, ex);
                }
                GC.Collect();
            }
        }

        public void Deactivate(string serviceId)
        {
            GlobalStore.ActivatedClassifiers.Remove(serviceId);
            GC.Collect();
        }

        public void Delete(Elastic.Models.ServiceElastic serviceElastic)
        {
            if (serviceElastic.Status == (int)ServiceStatusEnum.Active)
            {
                Deactivate(serviceElastic.Id);
            }
            if (serviceElastic.Status == (int)ServiceStatusEnum.Busy)
            {
                var actualProcessId = serviceElastic.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
                if (actualProcessId != null)
                {
                    processHandler.Cancel(actualProcessId);
                    while (GlobalStore.Processes.IsExist(actualProcessId)) { }
                }
                
            }
            if (serviceElastic.Status == (int)ServiceStatusEnum.Prepared) { }

            var directoryPath = string.Format("{0}/{1}", _dictionaryRootPath, serviceElastic.Id);
            IOHelper.SafeDeleteDictionary(directoryPath, true);
        }

        public void ExportDictionaries(string processId, ClassifierSettingsElastic settings, List<string> tagIdList, List<int> nGramList, CancellationToken token, string hostUrl)
        {
            try
            {
                var service = serviceQuery.Get(settings.ServiceId);

                var dataSet = GlobalStore.DataSets.Get(settings.DataSetName).DataSet;
                var allDicCount = tagIdList.Count * nGramList.Count;
                /*ZIP time*/allDicCount += (allDicCount / 10);
                var counter = 0;

                foreach (var nGram in nGramList)
                {
                    var dictionariesPath = string.Format("{0}/{1}/{2}", _dictionaryRootPath, settings.ServiceId, nGram);
                    var tempDirectoryPath = string.Format("{0}/{1}/{2}", siteConfig.Directory.Temp, processId, nGram);

                    Directory.CreateDirectory(tempDirectoryPath);

                    foreach (var tagId in tagIdList)
                    {
                        if (token.IsCancellationRequested)
                        {
                            processHandler.Cancelled(processId);
                            return;
                        }
                        var filePath = $"{dictionariesPath}/{DictionaryProtoBuf.GetFileName(tagId)}";
                        var dicProtoBuf = BaseProtoBuf.DeSerialize<DictionaryProtoBuf>(filePath);

                        var csvPath = $"{tempDirectoryPath}/{tagId}.csv";
                        if (dicProtoBuf.Dictionary != null)
                        {
                            CsvHelper.CreateCsv(csvPath, dicProtoBuf.Dictionary.Select(d => new List<string> { d.Key, d.Value.ToString() }).ToList());
                        }
                        else
                        {
                            CsvHelper.CreateCsv(csvPath, new List<List<string>>());
                        }

                        if (++counter % 15 == 0) processHandler.Changed(processId, Math.Round(counter / (double)allDicCount * 100, 2));
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
                        string.Format(ServiceResources.SuccessfullyExportedDictionariesFrom_0_Service_1, ServiceTypeEnum.Classifier, service.Name),
                        string.Format(ServiceResources.ExportFileCanBeDownloadFromHere_0, zipUrl)));

            }
            catch (Exception ex)
            {
                processHandler.Interrupted(processId, ex);
            }
        }

        public IEnumerable<ClassifierRecommendationResult> Recommend(string id, string originalText, int count, bool useEmphasizing, bool needTagInResult) {
            var analyzeQuery = queryFactory.GetAnalyzeQuery(GlobalStore.ActivatedClassifiers.Get(id).ClassifiersSettings.DataSetName);
            //a bi/tri stb gramokat nem jobb lenne elastic-al? Jelenleg a Scorer csinálja az NGramMaker-el
            var tokens = analyzeQuery.Analyze(originalText, 1).ToList();
            var text = string.Join(" ", tokens);

            var allResults = new ConcurrentBag<KeyValuePair<string, double>>();
            foreach (var scorerKvp in GlobalStore.ActivatedClassifiers.Get(id).ClassifierScorers)
            {
                var score = scorerKvp.Value.GetScore(text, 1.7, true);
                allResults.Add(new KeyValuePair<string, double>(scorerKvp.Key, score));
            }

            var resultsList = allResults.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ToList();

            var emphasizedCategoriesDictionary = new Dictionary<string, string>();
            if (useEmphasizing)
            {
                emphasizedCategoriesDictionary = resultsList.Where(r =>
                    GlobalStore.ActivatedClassifiers.Get(id).ClassifierEmphasizedTagIds.ContainsKey(r.Key) &&
                    GlobalStore.ActivatedClassifiers.Get(id).ClassifierEmphasizedTagIds[r.Key].All(word => tokens.Contains(word)))
                    .ToDictionary(r => r.Key, r => r.Key);

                resultsList = resultsList.OrderByDescending(r => emphasizedCategoriesDictionary.ContainsKey(r.Key) ? (r.Value + 100) : r.Value).ToList();
            }
            if (count != 0 && resultsList.Count > count) resultsList = resultsList.Take(count).ToList();

            var results = resultsList.Select(r => new ClassifierRecommendationResult
            {
                TagId = r.Key,
                Score = r.Value,
                Tag = needTagInResult ? GlobalStore.ActivatedClassifiers.Get(id).ClassifiersTags[r.Key] : null,
                IsEmphasized = useEmphasizing && emphasizedCategoriesDictionary.ContainsKey(r.Key)
            });
            return results;
        }
    }
}