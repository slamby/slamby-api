using Nest;
using Slamby.Elastic.Models;
using System.Collections.Generic;
using System.Linq;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Common.Config;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    [TransientDependency(ServiceType = typeof(IEnsureIndex))]
    public class ProcessQuery : BaseQuery, IEnsureIndex
    {
        readonly ElasticClientFactory elasticClientFactory;
        readonly IndexQuery indexQuery;

        public ProcessQuery(ElasticClientFactory elasticClientFactory, IndexQuery indexQuery, SiteConfig siteConfig) : 
            base(elasticClientFactory, Constants.SlambyProcessesIndex, siteConfig)
        {
            this.indexQuery = indexQuery;
            this.elasticClientFactory = elasticClientFactory;
        }

        public void CreateIndex()
        {
            if (indexQuery.IsExists(Constants.SlambyProcessesIndex))
            {
                return;
            }

            var client = elasticClientFactory.GetClient();

            var descriptor = new CreateIndexDescriptor(Constants.SlambyProcessesIndex);
            descriptor
                .Settings(s => s.
                    NumberOfReplicas(0)
                    .NumberOfShards(1))
                .Mappings(m => m
                    .Map<ProcessElastic>(mm => mm.AutoMap().Dynamic(false))
                    );

            var createResp = client.CreateIndex(descriptor);
            ResponseValidator(createResp);

            var propDesc = new PropertiesDescriptor<ProcessElastic>();
            propDesc.Object<object>(s => s.Name(ProcessElastic.InitObjectMappingName).Dynamic(true));
            var putMappingDesc = new PutMappingDescriptor<ProcessElastic>()
                .Index(Constants.SlambyProcessesIndex)
                .Dynamic(DynamicMapping.Strict)
                .Properties(p => propDesc);
            var mapResp = client.Map<DocumentElastic>(p => putMappingDesc);
            ResponseValidator(mapResp);
        }

        public IEnumerable<ProcessElastic> GetAll(bool justActives, int lastDays = 0)
        {
            var sdesc = new SearchDescriptor<ProcessElastic>();
            var queryContDesc = new QueryContainerDescriptor<ProcessElastic>();
            var queryContainers = new List<QueryContainer>();
            if (justActives)
            {
                queryContainers.Add(queryContDesc
                    .Term(t => t
                        .Field(f => f.Status)
                        .Value((int)SDK.Net.Models.Enums.ProcessStatusEnum.InProgress)));
            }
            if (lastDays > 0)
            {
                queryContainers.Add(queryContDesc
                    .DateRange(dr => dr
                        .Field(f => f.Start)
                        .GreaterThanOrEquals(System.DateTime.UtcNow.AddDays(-lastDays))));
            }
            if (queryContainers.Count > 0)
            {
                sdesc.Query(query => query
                .Bool(selector => selector
                    .Must(queryContainers.ToArray())));
            }
            return Get(sdesc).Items;
        }

        public ProcessElastic Get(string id)
        {
            var sdesc = new SearchDescriptor<ProcessElastic>().Query(q => q.Ids(i => i.Values(id)));
            return Get(sdesc).Items.FirstOrDefault();
        }

        public void Index(ProcessElastic processElastic)
        {
            Index(new List<ProcessElastic> { processElastic });
        }

        public void Index(IEnumerable<ProcessElastic> processElastics)
        {
            if (!processElastics.Any())
            {
                return;
            }

            var response = Client.IndexMany(processElastics);
            ResponseValidator(response);
            Client.Flush(IndexName);
        }

        public string Update(string id, ProcessElastic processElastic)
        {
            var response = Client.Update(new DocumentPath<ProcessElastic>(id), ur => ur.Doc(processElastic));
            ResponseValidator(response);
            return response.Id;
        }

        public bool Delete(string id)
        {
            var deleteResponse = Client.Delete<ProcessElastic>(id);
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
            return true;
        }

        public bool Delete(List<string> ids)
        {
            if (!ids.Any()) return true;
            var deleteResponse = Client.DeleteMany<ProcessElastic>(ids.Select(id => new ProcessElastic { Id = id }));
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
            return true;
        }
        public bool IsExists(string id)
        {
            return Client.DocumentExists<ProcessElastic>(id).Exists;
        }

        /// <summary>
        /// TODO: remove after every API upgraded to 1.0
        /// It is used one time only for migration
        /// </summary>
        public void RelocateFromServices()
        {
            var sdesc = new SearchDescriptor<ProcessElastic>().Index(Constants.SlambyServicesIndex);
            var items = Get(sdesc).Items;
            Index(items);
        }
    }
}
