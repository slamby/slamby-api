using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Queries;

namespace Slamby.Elastic.Factories
{
    [SingletonDependency(ServiceType = typeof(IQueryFactory))]
    public class QueryFactory : IQueryFactory
    {
        readonly ElasticClientFactory elasticClientFactory;
        readonly SiteConfig siteConfig;

        public QueryFactory(ElasticClientFactory elasticClientFactory, SiteConfig siteConfig)
        {
            this.elasticClientFactory = elasticClientFactory;
            this.siteConfig = siteConfig;
        }

        public IndexQuery GetIndexQuery(string name)
        {
            return new IndexQuery(elasticClientFactory.GetClient(name), elasticClientFactory, siteConfig);
        }

        public IndexQuery GetIndexQuery()
        {
            return new IndexQuery(elasticClientFactory.GetClient(), elasticClientFactory, siteConfig);
        }

        public WordQuery GetWordQuery(string name)
        {
            return new WordQuery(elasticClientFactory.GetClient(name), siteConfig);
        }

        public TagQuery GetTagQuery(string name)
        {
            return new TagQuery(elasticClientFactory.GetClient(name), siteConfig);
        }

        public AnalyzeQuery GetAnalyzeQuery(string name)
        {
            return new AnalyzeQuery(elasticClientFactory.GetClient(name), siteConfig);
        }

        public IDocumentQuery GetDocumentQuery(string name)
        {
            return new DocumentQuery(elasticClientFactory.GetClient(name), siteConfig);
        }

        public IDocumentQuery GetDocumentQuery()
        {
            return new DocumentQuery(elasticClientFactory.GetClient(), siteConfig);
        }

        public OptimizeQuery GetOptimizeQuery(string name)
        {
            return new OptimizeQuery(elasticClientFactory.GetClient(name), siteConfig);
        }
    }
}
