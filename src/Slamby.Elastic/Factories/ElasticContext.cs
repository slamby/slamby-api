using System;
using System.Diagnostics;
using System.Linq;
using Elasticsearch.Net;
using Nest;
using Slamby.Common.Config;
using Slamby.Common.DI;

namespace Slamby.Elastic.Factories
{
    [ScopedDependency]
    public class ElasticContext
    {
        readonly SiteConfig siteConfig;

        public bool DebugMode { get; set; } = false;

        public string Index { get; set; } = string.Empty;

        public ElasticContext(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
        }

        public ElasticClient GetClient()
        {
            return GetClient(Index);
        }

        public ElasticClient GetClient(string defaultIndex)
        {
            var uris = siteConfig.ElasticSearch.Uris.Select(uri => new Uri(uri)).ToList();
            var connectionPool = new SingleNodeConnectionPool(uris.First());
            var settings = new ConnectionSettings(connectionPool, new Common.SlambyConnection.SlambyHttpConnection());

            settings.MaximumRetries(5);
            settings.RequestTimeout(new TimeSpan(0, 10, 0));

            if (defaultIndex != null)
            {
                settings.DefaultIndex(defaultIndex);
            }

            if (DebugMode)
            {
                EnableLogging(settings);
            }

            var _client = new ElasticClient(settings);
            return _client;
        }

        private void EnableLogging(ConnectionSettings settings)
        {
            // Logging
            settings.DisableDirectStreaming();
            settings.OnRequestCompleted(acd =>
            {
                var path = acd.Uri.PathAndQuery;

                if (path != "/_cluster/health" && acd.RequestBodyInBytes != null)
                {
                    Debug.WriteLine(System.Text.Encoding.UTF8.GetString(acd.RequestBodyInBytes));
                    Debug.WriteLine(System.Text.Encoding.UTF8.GetString(acd.ResponseBodyInBytes));
                }
            });
        }
    }
}
