using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nest;
using Slamby.Common;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models.Enums;

namespace Slamby.API.Services
{
    [TransientDependency]
    public class DBUpdateService
    {
        readonly ElasticClientFactory clientFactory;
        readonly IndexQuery indexQuery;
        readonly IQueryFactory queryFactory;
        readonly ILogger logger;
        readonly MetadataQuery metadataQuery;
        readonly ServiceQuery serviceQuery;
        readonly ProcessQuery processQuery;
        readonly ServiceManager serviceManager;

        public DBUpdateService(ElasticClientFactory clientFactory, IndexQuery indexQuery, IQueryFactory queryFactory,
            ILoggerFactory loggerFactory, MetadataQuery metadataQuery, ServiceQuery serviceQuery, ProcessQuery processQuery,
            ServiceManager serviceManager)
        {
            this.serviceManager = serviceManager;
            this.processQuery = processQuery;
            this.serviceQuery = serviceQuery;
            this.metadataQuery = metadataQuery;
            this.queryFactory = queryFactory;
            this.indexQuery = indexQuery;
            this.clientFactory = clientFactory;
            this.logger = loggerFactory.CreateLogger<DBUpdateService>();
        }

        private IEnumerable<string> GetIndexes()
        {
            var cats = indexQuery.GetCats();
            while (cats == null)
            {
                cats = indexQuery.GetCats();
            }

            return cats.Select(stat => stat.Key);
        }

        public void UpdateDatabase()
        {
            UpdateDataSets();

            // We have up-to-date datasets (version 2)
            // DataSet Properties DBVersion is not used from this point

            UpdateVersion(0, 3, () =>
            {
                logger.LogInformation("Adding Service Alias field");
                MapModelElastic<ServiceElastic>(Elastic.Constants.SlambyServicesIndex);
            });

            UpdateVersion(3, () =>
            {
                logger.LogInformation($"Service Alias mapping to Not Analyzed with recreating {Elastic.Constants.SlambyServicesIndex}...");
                serviceQuery.ReCreateIndex();
            });

            UpdateVersion(4, () =>
            {
                logger.LogInformation("Adding Compress Settings field");
                MapModelElastic<ClassifierSettingsElastic>(Elastic.Constants.SlambyServicesIndex);
                MapModelElastic<PrcSettingsElastic>(Elastic.Constants.SlambyServicesIndex);
            });

            UpdateVersion(5, () =>
            {
                MapModelElastic<PrcSettingsElastic>(Elastic.Constants.SlambyServicesIndex);
            });

            UpdateVersion(6, () =>
            {
                serviceManager.UpdateDataSetNameToIndex<PrcSettingsElastic>(ServiceTypeEnum.Prc);
                serviceManager.UpdateDataSetNameToIndex<ClassifierSettingsElastic>(ServiceTypeEnum.Classifier);
            });
        }

        private void MapModelElastic<TModelElastic>(string index) where TModelElastic : class
        {
            var elasticClient = clientFactory.GetClient(index);
            var mapresponse = elasticClient.Map<TModelElastic>(m => m
                .AutoMap()
                .Dynamic(DynamicMapping.Strict));
            if (!mapresponse.Acknowledged)
            {
                throw new Exception("ElasticSearch not acknowledged the Update!");
            }
        }

        private void UpdateVersion(int fromVersion, Action updateAction)
        {
            UpdateVersion(fromVersion, fromVersion + 1, updateAction);
        }

        private void UpdateVersion(int fromVersion, int toVersion, Action updateAction)
        {
            var metadataHit = metadataQuery.GetHit();
            var metadataElastic = metadataHit.Source;

            if (metadataElastic.DBVersion > fromVersion)
            {
                return;
            }

            logger.LogInformation($"Updating from version {fromVersion} using Metadata...");

            updateAction();

            metadataElastic.DBVersion = toVersion;
            metadataQuery.Index(metadataHit.Id, metadataElastic);

            logger.LogInformation($"Updated to version {toVersion} successfully");
        }

        private void UpdateDataSets()
        {
            foreach (var indexName in GetIndexes())
            {
                var elasticClient = clientFactory.GetClient(indexName);
                var hits = queryFactory.GetIndexQuery(indexName).GetPropertiesHit(indexName);

                if (!hits.ContainsKey(indexName))
                {
                    logger.LogCritical($"The dataset {indexName} is in inconsistent state!");
                    continue;
                }

                var propertiesElastic = hits[indexName].Source;

#pragma warning disable CS0618 // Type or member is obsolete
                if (propertiesElastic.DBVersion < Constants.DBVersion)
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    logger.LogInformation("Elasticsearch database is not at the latest version");

                    // Update from 1 -> 2
#pragma warning disable CS0618 // Type or member is obsolete
                    if (propertiesElastic.DBVersion == 1)
#pragma warning restore CS0618 // Type or member is obsolete
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        logger.LogInformation($"Updating from version {propertiesElastic.DBVersion}...");
#pragma warning restore CS0618 // Type or member is obsolete
                        propertiesElastic.Name = string.Empty;
#pragma warning disable CS0618 // Type or member is obsolete
                        propertiesElastic.DBVersion = 2;
#pragma warning restore CS0618 // Type or member is obsolete

                        elasticClient.Index(propertiesElastic, desc => desc.Id(hits[indexName].Id));
                        elasticClient.Flush(indexName);

#pragma warning disable CS0618 // Type or member is obsolete
                        logger.LogInformation($"Updated to version {propertiesElastic.DBVersion} successfully");
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                }
            }
        }

        public void UpdateReplicaNumbers(int repNum)
        {
            var elasticClient = clientFactory.GetClient();
            elasticClient.UpdateIndexSettings(Indices.All, i => i.IndexSettings(iset => iset.NumberOfReplicas(repNum)));
        }
    }
}
