using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Slamby.API.Helpers.Services.Interfaces;
using Slamby.API.Models;
using Slamby.API.Models.Serializable;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Cerebellum;
using Slamby.Cerebellum.Dictionary;
using Slamby.Common;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;
using StackExchange.Redis;
using Slamby.SDK.Net.Models.Services;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Helpers.Services
{
    [TransientDependency]
    public class PrcServiceHandler : ITypedServiceHandler<PrcSettingsElastic>
    {
        private readonly string _dictionaryRootPath;
        readonly SiteConfig siteConfig;
        readonly ServiceQuery serviceQuery;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;
        readonly ParallelService parallelService;
        readonly MachineResourceService machineResourceService;
        readonly ServiceHandler serviceHandler;

        public string GetDirectoryPath(string serviceId) => $"{_dictionaryRootPath}/{serviceId}";

        public IGlobalStoreManager GlobalStore { get; set; }

        readonly ILogger<PrcServiceHandler> logger;

        public PrcServiceHandler(SiteConfig siteConfig, ServiceQuery serviceQuery, ProcessHandler processHandler, IQueryFactory queryFactory,
            ParallelService parallelService, MachineResourceService machineResourceService, IGlobalStoreManager globalStore,
            ILogger<PrcServiceHandler> logger, ServiceHandler serviceHandler)
        {
            this.logger = logger;
            GlobalStore = globalStore;
            this.parallelService = parallelService;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.serviceQuery = serviceQuery;
            this.siteConfig = siteConfig;
            this.machineResourceService = machineResourceService;
            _dictionaryRootPath = siteConfig.Directory.Prc;
            this.serviceHandler = serviceHandler;
        }

        public PrcService Get(string id, bool withSettings = true)
        {
            var service = serviceHandler.Get<PrcService>(id);
            if (service == null) return null;

            PrcActivateSettings activateSettings = null;
            PrcPrepareSettings prepareSettings = null;
            PrcIndexSettings indexSettings = null;

            var prcSettingsElastic = withSettings ? serviceQuery.GetSettings<PrcSettingsElastic>(service.Id) : null;
            if (prcSettingsElastic != null)
            {
                if (service.Status == ServiceStatusEnum.Prepared || service.Status == ServiceStatusEnum.Active)
                {
                    prepareSettings = new PrcPrepareSettings
                    {
                        DataSetName = GlobalStore.DataSets.Get(prcSettingsElastic.DataSetName).AliasName,
                        TagIdList = prcSettingsElastic.Tags.Select(t => t.Id).ToList(),
                        CompressSettings = CompressHelper.ToCompressSettings(prcSettingsElastic.CompressSettings),
                        CompressLevel = CompressHelper.ToCompressLevel(prcSettingsElastic.CompressSettings)
                    };
                    if (service.Status == ServiceStatusEnum.Active)
                    {
                        activateSettings = new PrcActivateSettings
                        {
                            FieldsForRecommendation = prcSettingsElastic.FieldsForRecommendation
                        };

                        if (prcSettingsElastic?.IndexSettings?.IndexDate != null)
                        {
                            indexSettings = new PrcIndexSettings()
                            {
                                Filter = new Filter()
                                {
                                    Query = prcSettingsElastic.IndexSettings.FilterQuery,
                                    TagIdList = prcSettingsElastic.IndexSettings.FilterTagIdList
                                }
                            };
                        }
                    }
                }
            }
            service.ActivateSettings = activateSettings;
            service.PrepareSettings = prepareSettings;
            service.IndexSettings = indexSettings;

            return service;
        }

        public void Prepare(string processId, PrcSettingsElastic settings, CancellationToken token)
        {
            var directoryPath = GetDirectoryPath(settings.ServiceId);

            try
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Busy;
                serviceHandler.Update(service.Id, service);

                IOHelper.SafeDeleteDictionary(directoryPath, true);

                var globalStoreDataSet = GlobalStore.DataSets.Get(settings.DataSetName);
                var dataSet = globalStoreDataSet.DataSet;
                var progress = new Progress(settings.Tags.Count);
                var subsetCreator = new SubsetCreator(dataSet.Name, new List<string> { DocumentElastic.TextField }, dataSet.InterpretedFields.Select(DocumentQuery.MapDocumentObjectName).ToList(), 1, queryFactory, globalStoreDataSet.AttachmentFields);

                Directory.CreateDirectory(directoryPath);

                var logPrefix = $"Prc Prepare {processId}";
                logger.LogInformation($"{logPrefix} starts with ParallelLimit: {parallelService.ParallelLimit}, Tags Count: {settings.Tags.Count}");

                var lockObject = new object();

                Parallel.ForEach(settings.Tags, parallelService.ParallelOptions(), (tag, loopState) =>
                {
                    token.ThrowIfCancellationRequested();

                    logger.LogInformation($"{logPrefix} preparing Tag: `{tag}`");

                    var subset = subsetCreator.CreateByTag(tag.Id, dataSet.TagField);
                    var algorithm = new TwisterAlgorithm(
                            subset, true, true,
                            settings.CompressSettings.CompressCategoryOccurence,
                            settings.CompressSettings.CompressDataSetOccurence,
                            (LogicalOperatorEnum)settings.CompressSettings.CompressOperator);


                    algorithm.InitTagDictionary();
                    var notNeededWords = subset.WordsWithOccurences.Keys.Except(
                            algorithm.TagDictionary
                            .Where(sd => sd.Value.PMI > 0)
                            .Select(sd => sd.Key)).ToList();

                    var td = algorithm.GetDictionary();

                    foreach (var word in notNeededWords)
                    {
                        subset.WordsWithOccurences.Remove(word);
                    }

                    lock (lockObject)
                    {
                        //dictionary serialization
                        var dicProtoBuf = new DictionaryProtoBuf
                        {
                            Id = tag.Id,
                            Dictionary = td,
                            NGram = 1
                        };
                        dicProtoBuf.Serialize(string.Format("{0}/{1}", directoryPath, dicProtoBuf.GetFileName()));

                        //subset serialization
                        var subsetProtoBuf = new SubsetProtoBuf
                        {
                            Id = tag.Id,
                            WordsWithOccurences = subset.WordsWithOccurences,
                            AllWordsOccurencesSumInTag = subset.AllWordsOccurencesSumInTag,
                            AllOccurencesSumInCorpus = subset.AllWordsOccurencesSumInCorpus
                        };
                        subsetProtoBuf.Serialize(string.Format("{0}/{1}", directoryPath, subsetProtoBuf.GetFileName()));

                        progress.Step();
                        processHandler.Changed(processId, progress.Percent.Round(2));
                    }

                    logger.LogInformation($"{logPrefix} prepared Tag: `{tag}`");
                });

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyPrepared_0_Service_1, ServiceTypeEnum.Prc, service.Name));
                service.Status = ServiceStatusEnum.Prepared;
                serviceHandler.Update(service.Id, service);
            }
            catch (Exception ex)
            {

                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.New;
                serviceHandler.Update(service.Id, service);
                IOHelper.SafeDeleteDictionary(directoryPath, true);
                if (ex.InnerException != null && ex.InnerException is OperationCanceledException)
                {
                    processHandler.Cancelled(processId);
                }
                else
                {
                    processHandler.Interrupted(processId, ex);
                }
            }
        }

        public void Activate(string processId, PrcSettingsElastic settings, CancellationToken token)
        {
            try
            {
                GC.Collect();
                machineResourceService.UpdateResourcesManually();
                var freeMemInBytes = machineResourceService.Status.FreeMemory * 1024 * 1024;

                var directoryPath = string.Format("{0}/{1}", _dictionaryRootPath, settings.ServiceId);
                var dictionaryPaths = IOHelper.GetFilesInFolder(directoryPath, DictionaryProtoBuf.GetExtension());

                var subsetPaths = new List<string>();
                subsetPaths.AddRange(IOHelper.GetFilesInFolder(directoryPath, SubsetProtoBuf.GetExtension()));

                var sizeInBytes = dictionaryPaths.Sum(f => new FileInfo(f).Length);
                sizeInBytes += subsetPaths.Sum(f => new FileInfo(f).Length);

                if (freeMemInBytes > 0 && freeMemInBytes < sizeInBytes * Constants.DictionaryInMemoryMultiplier)
                {
                    throw new Common.Exceptions.OutOfResourceException(ServiceResources.NotEnoughResourceToActivateService);
                }

                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Busy;
                serviceHandler.Update(service.Id, service);

                var deserializedDics = new ConcurrentBag<DictionaryProtoBuf>();
                var deserializedSubsets = new ConcurrentBag<SubsetProtoBuf>();

                var lockObject = new object();
                var allCount = dictionaryPaths.Count + subsetPaths.Count;
                var progress = new Progress(allCount);

                var allPaths = new List<string>();
                allPaths.AddRange(dictionaryPaths);
                allPaths.AddRange(subsetPaths);

                var dicPathsDic = new ConcurrentDictionary<string, string>(dictionaryPaths.ToDictionary(p => p, p => p));
                var subsetPathsDic = new ConcurrentDictionary<string, string>(subsetPaths.ToDictionary(p => p, p => p));

                Parallel.ForEach(allPaths, parallelService.ParallelOptions(), (path, loopState) =>
                {
                    token.ThrowIfCancellationRequested();

                    if (dicPathsDic.ContainsKey(path)) deserializedDics.Add(BaseProtoBuf.DeSerialize<DictionaryProtoBuf>(path));
                    else if (subsetPathsDic.ContainsKey(path)) deserializedSubsets.Add(BaseProtoBuf.DeSerialize<SubsetProtoBuf>(path));

                    lock (lockObject)
                    {
                        progress.Step();
                        if (progress.Value % 15 == 0) processHandler.Changed(processId, progress.Percent.Round(2));
                    }
                });

                var globalStorePrc = new GlobalStorePrc();

                if (deserializedDics.Any())
                {
                    var scorersDic = deserializedDics.GroupBy(d => d.Id).ToDictionary(d => d.Key, d => new Cerebellum.Scorer.PeSScorer(d.ToDictionary(di => di.NGram, di => di.Dictionary)));
                    globalStorePrc.PrcScorers = scorersDic;
                }

                if (deserializedSubsets.Any())
                {
                    var subsetsDic = deserializedSubsets.ToDictionary(d => d.Id, d => new Subset
                    {
                        AllWordsOccurencesSumInCorpus = d.AllOccurencesSumInCorpus,
                        AllWordsOccurencesSumInTag = d.AllWordsOccurencesSumInTag,
                        WordsWithOccurences = d.WordsWithOccurences
                    });
                    globalStorePrc.PrcSubsets = subsetsDic;
                }

                globalStorePrc.PrcsSettings = settings;

                GlobalStore.ActivatedPrcs.Add(settings.ServiceId, globalStorePrc);

                processHandler.Finished(processId, string.Format(ServiceResources.SuccessfullyActivated_0_Service_1, ServiceTypeEnum.Prc, service.Name));
                service.Status = ServiceStatusEnum.Active;
                serviceHandler.Update(service.Id, service);
            }
            catch (Exception ex)
            {
                var service = Get(settings.ServiceId);
                service.Status = ServiceStatusEnum.Prepared;
                serviceHandler.Update(service.Id, service);
                if (GlobalStore.ActivatedPrcs.IsExist(settings.ServiceId)) GlobalStore.ActivatedPrcs.Remove(settings.ServiceId);
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
            GlobalStore.ActivatedPrcs.Remove(serviceId);
            GC.Collect();
        }

        public void Delete(Service service)
        {
            if (service.Status == ServiceStatusEnum.Active)
            {
                Deactivate(service.Id);
            }
            if (service.Status == ServiceStatusEnum.Busy)
            {
                var actualProcessId = service.ProcessIdList.FirstOrDefault(pid => GlobalStore.Processes.IsExist(pid));
                if (actualProcessId != null)
                {
                    processHandler.Cancel(actualProcessId);
                    while (GlobalStore.Processes.IsExist(actualProcessId)) { }
                }
            }
            if (service.Status == ServiceStatusEnum.Prepared) { }

            var directoryPath = string.Format("{0}/{1}", _dictionaryRootPath, service.Id);
            IOHelper.SafeDeleteDictionary(directoryPath, true);
        }

        public void ExportDictionaries(string processId, PrcSettingsElastic settings, List<string> tagIdList, CancellationToken token, string hostUrl)
        {
            try
            {
                var service = Get(settings.ServiceId);

                var dataSet = GlobalStore.DataSets.Get(settings.DataSetName).DataSet;
                var allDicCount = tagIdList.Count;
                /*ZIP time*/
                allDicCount += (allDicCount / 10);
                var progress = new Progress(allDicCount);

                var dictionariesPath = string.Format("{0}/{1}", _dictionaryRootPath, settings.ServiceId);

                var tempDirectoryPath = string.Format("{0}/{1}", siteConfig.Directory.Temp, processId);
                System.IO.Directory.CreateDirectory(tempDirectoryPath);

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

                    progress.Step();
                    processHandler.Changed(processId, progress.Percent.Round(2));
                }
                /*time to ZIP the results*/
                var zipFileName = string.Format("{0}.zip", processId);
                var dirToZipPath = string.Format("{0}/{1}", siteConfig.Directory.Temp, processId);
                var resultZipPath = string.Format("{0}/{1}", siteConfig.Directory.User, zipFileName);
                ZipHelper.CompressFolder(dirToZipPath, resultZipPath);

                var zipUrl = string.Format("{0}{1}/{2}", hostUrl, Common.Constants.FilesPath, zipFileName);

                processHandler.Finished(processId,
                    string.Format("{0}\n{1}",
                        string.Format(ServiceResources.SuccessfullyExportedDictionariesFrom_0_Service_1, ServiceTypeEnum.Prc, service.Name),
                        string.Format(ServiceResources.ExportFileCanBeDownloadFromHere_0, zipUrl)));

            }
            catch (Exception ex)
            {
                processHandler.Interrupted(processId, ex);
            }
        }
    }
}
