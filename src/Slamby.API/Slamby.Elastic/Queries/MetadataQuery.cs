using System.Linq;
using Nest;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Models;
using Slamby.Common.Config;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    [TransientDependency(ServiceType = typeof(IEnsureIndex))]
    public class MetadataQuery : BaseQuery, IEnsureIndex
    {
        readonly IndexQuery indexQuery;
        readonly ElasticClientFactory elasticClientFactory;

        public MetadataQuery(ElasticClientFactory elasticClientFactory, IndexQuery indexQuery, SiteConfig siteConfig) : 
            base(elasticClientFactory, Constants.SlambyMetadataIndex, siteConfig)
        {
            this.elasticClientFactory = elasticClientFactory;
            this.indexQuery = indexQuery;
        }

        public void CreateIndex()
        {
            if (indexQuery.IsExists(Constants.SlambyMetadataIndex))
            {
                return;
            }

            var client = elasticClientFactory.GetClient();

            var descriptor = new CreateIndexDescriptor(Constants.SlambyMetadataIndex);
            descriptor
                .Settings(s => s
                    .NumberOfReplicas(0)
                    .NumberOfShards(1))
                .Mappings(m => m
                    .Map<MetadataElastic>(mm => mm
                        .AutoMap()
                        .Dynamic(false)
                        ));

            var createResp = client.CreateIndex(descriptor);
            ResponseValidator(createResp);

            var metadataElastic = new MetadataElastic()
            {
                DBVersion = 0
            };
            Index(metadataElastic);
        }

        public MetadataElastic Get()
        {
            return GetHit()?.Source;
        }

        public IHit<MetadataElastic> GetHit()
        {
            var client = elasticClientFactory.GetClient(Constants.SlambyMetadataIndex);
            var sdesc = new SearchDescriptor<MetadataElastic>().MatchAll();

            return client.Search<MetadataElastic>(sdesc).Hits.FirstOrDefault();
        }

        public void Index(MetadataElastic metadataElastic)
        {
            var response = Client.Index(metadataElastic);
            ResponseValidator(response);
            Client.Flush(IndexName);
        }

        public void Index(string id, MetadataElastic metadataElastic)
        {
            var response = Client.Index(metadataElastic, desc => desc.Id(id));
            ResponseValidator(response);
            Client.Flush(IndexName);
        }
    }
}
