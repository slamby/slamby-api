using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;

namespace Slamby.Elastic.Factories
{
    [TransientDependency]
    public class ElasticClientFactory
    {
        public static ElasticClient GetClient(IServiceProvider serviceProvider)
        {
            var siteConfig = serviceProvider.GetService<SiteConfig>();
            var dataSetSelector = serviceProvider.GetService<IDataSetSelector>();
            var uriList = siteConfig.ElasticSearch.Uris.Select(uri => new Uri(uri)).ToList();

            return GetClient(uriList, dataSetSelector.DataSetName);
        }

        readonly SiteConfig siteConfig;

        public ElasticClientFactory(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
        }

        public ElasticClient GetClient(string dataSetName)
        {
            return GetClient(siteConfig.ElasticSearch.Uris.Select(uri => new Uri(uri)).ToList(), dataSetName);
        }

        public ElasticClient GetClient()
        {
            return GetClient(siteConfig.ElasticSearch.Uris.Select(uri => new Uri(uri)).ToList(), null);
        }

        public static ElasticClient GetClient(List<Uri> uris, string indexName)
        {
            var connectionPool = new SingleNodeConnectionPool(uris.First());
            var settings = new ConnectionSettings(connectionPool, new Common.SlambyConnection.SlambyHttpConnection());

            settings.MaximumRetries(5);
            settings.RequestTimeout(new TimeSpan(0, 10, 0));

            if (indexName != null)
            {
                settings.DefaultIndex(indexName);
            }

            //// Logging
            //settings.DisableDirectStreaming();
            //settings.OnRequestCompleted(acd =>
            //{
            //    var path = acd.Uri.PathAndQuery;

            //    if (path != "/_cluster/health" && acd.RequestBodyInBytes != null)
            //    {
            //        Debug.WriteLine(System.Text.Encoding.UTF8.GetString(acd.RequestBodyInBytes));
            //        Debug.WriteLine(System.Text.Encoding.UTF8.GetString(acd.ResponseBodyInBytes));
            //    }
            //});

            var _client = new ElasticClient(settings);
            return _client;
        }
    }
}
