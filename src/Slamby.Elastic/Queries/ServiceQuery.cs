using System.Collections.Generic;
using System.Linq;
using Nest;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Models;
using Slamby.Common.Config;
using System;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    [TransientDependency(ServiceType = typeof(IEnsureIndex))]
    public class ServiceQuery : BaseQuery, IEnsureIndex
    {
        readonly ElasticClientFactory elasticClientFactory;
        readonly IndexQuery indexQuery;

        public ServiceQuery(ElasticClientFactory elasticClientFactory, IndexQuery indexQuery, SiteConfig siteConfig) : 
            base(elasticClientFactory, Constants.SlambyServicesIndex, siteConfig)
        {
            this.indexQuery = indexQuery;
            this.elasticClientFactory = elasticClientFactory;
        }

        public void CreateIndex()
        {
            if (indexQuery.IsExists(Constants.SlambyServicesIndex))
            {
                return;
            }

            var descriptor = new CreateIndexDescriptor(Constants.SlambyServicesIndex);
            descriptor
                .Settings(s => s
                    .NumberOfReplicas(0)
                    .NumberOfShards(1))
                .Mappings(m => m
                    .Map<ServiceElastic>(mm => mm.AutoMap().Dynamic(false))
                    .Map<ClassifierSettingsElastic>(mm => mm.AutoMap().Dynamic(false))
                    .Map<PrcSettingsElastic>(mm => mm.AutoMap().Dynamic(false)));

            var createResp = elasticClientFactory.GetClient().CreateIndex(descriptor);
            ResponseValidator(createResp);
        }

        public void ReCreateIndex()
        {
            var elasticClient = elasticClientFactory.GetClient(Elastic.Constants.SlambyServicesIndex);

            var services = GetAll();
            var classifierSettings = GetSettings<ClassifierSettingsElastic>();
            var prcSettings = GetSettings<PrcSettingsElastic>();

            var deleteresponse = elasticClient.DeleteIndex(Elastic.Constants.SlambyServicesIndex);
            if (!deleteresponse.Acknowledged)
            {
                throw new Exception("ElasticSearch not acknowledged the DeleteIndex!");
            }

            CreateIndex();

            Index(services);
            IndexSettings(classifierSettings);
            IndexSettings(prcSettings);
        }

        public IEnumerable<ServiceElastic> GetAll()
        {
            var sdesc = new SearchDescriptor<ServiceElastic>();
            return Get(sdesc).Items;
        }

        public ServiceElastic Get(string idOrAlias)
        {
             var sdesc = new SearchDescriptor<ServiceElastic>()
                .Query(q =>
                    q.Ids(i => i.Values(idOrAlias)) || 
                    q.MatchPhrase(m => m // matchphrase is case sensitive
                        .Field(f => f.Alias)
                        .Query(idOrAlias))
                );
            return Get(sdesc).Items.FirstOrDefault();
        }

        public ServiceElastic GetByAlias(string alias)
        {
            var sdesc = new SearchDescriptor<ServiceElastic>()
                .Query(q => q
                    .MatchPhrase(m => m // matchphrase is case sensitive
                        .Field(f => f.Alias)
                        .Query(alias))
                    );
            return Get(sdesc).Items.FirstOrDefault();
        }

        public IEnumerable<ServiceElastic> GetByType(int serviceType)
        {
            var sdesc = new SearchDescriptor<ServiceElastic>()
                .Query(q => q
                    .Term(t => t
                        .Field(f => f.Type)
                        .Value(serviceType)
                        )
                    );
            return Get(sdesc).Items;
        }

        public void Index(ServiceElastic serviceElastic)
        {
            Index(new List<ServiceElastic> { serviceElastic });
        }

        public void Index(IEnumerable<ServiceElastic> serviceElastics)
        {
            if (!serviceElastics.Any())
            {
                return;
            }

            var response = Client.IndexMany(serviceElastics);
            ResponseValidator(response);
            Client.Flush(IndexName);
        }

        public string Update(string id, ServiceElastic serviceElastic)
        {
            var response = Client.Update(new DocumentPath<ServiceElastic>(id), ur => ur.Doc(serviceElastic));
            ResponseValidator(response);
            Client.Flush(IndexName);
            return response.Id;
        }

        public bool Delete(string serviceId)
        {
            var deleteResponse = Client.Delete<ServiceElastic>(serviceId);
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
            return true;
        }

        //public bool Delete(string id)
        //{
        //    var deleteResponse = Client.Delete<ServiceElastic>(id);
        //    ResponseValidator(deleteResponse);
        //    ResponseValidator(Client.Flush(IndexName));
        //    return true;
        //}

        //public bool IsExists(string id)
        //{
        //    return Client.DocumentExists<ServiceElastic>(id).Exists;
        //}

        #region Settings

        public void IndexSettings<T>(T settingsElastic) where T : BaseServiceSettingsElastic
        {
            IndexSettings(new List<T> { settingsElastic }.AsEnumerable());
        }

        public void IndexSettings<T>(IEnumerable<T> settingsElastics) where T : BaseServiceSettingsElastic
        {
            if (!settingsElastics.Any())
            {
                return;
            }

            var response = Client.IndexMany(settingsElastics);
            ResponseValidator(response);
            Client.Flush(IndexName);
        }

        public T GetSettings<T>(string serviceId) where T : BaseServiceSettingsElastic
        {
            var sdesc = new SearchDescriptor<T>().Query(q => q.Ids(i => i.Values(serviceId)));
            var searchResponse = Client.Search<T>(sdesc);
            ResponseValidator(searchResponse);
            return searchResponse.Documents.FirstOrDefault();
        }

        public IEnumerable<TServiceSettings> GetSettingsByDataSet<TServiceSettings>(string dataSetName) 
            where TServiceSettings : BaseServiceSettingsElastic
        {
            var sdesc = new SearchDescriptor<TServiceSettings>()
                .Query(q => q
                    .Term(t => t
                        .Field(f => f.DataSetName)
                        .Value(dataSetName)
                        ));
            var searchResponse = Client.Search<TServiceSettings>(sdesc);
            ResponseValidator(searchResponse);
            return searchResponse.Documents;
        }

        public IEnumerable<TServiceSettings> GetSettings<TServiceSettings>()
            where TServiceSettings : BaseServiceSettingsElastic
        {
            var sdesc = new SearchDescriptor<TServiceSettings>();
            var searchResponse = Client.Search<TServiceSettings>(sdesc);
            ResponseValidator(searchResponse);
            return searchResponse.Documents;
        }

        public bool DeleteSettings<T>(string serviceId) where T : BaseServiceSettingsElastic
        {
            var sdesc = new SearchDescriptor<T>().Query(q => q.Ids(i => i.Values(serviceId)));
            var searchResponse = Client.Search<T>(sdesc);
            ResponseValidator(searchResponse);
            var settingsElastic = searchResponse.Documents.FirstOrDefault();
            if (settingsElastic == null) return true;
            var deleteResponse = Client.Delete<T>(serviceId);
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
            return true;
        }
        #endregion
    }
}
