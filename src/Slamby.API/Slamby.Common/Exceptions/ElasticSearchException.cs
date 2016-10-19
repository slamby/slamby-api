using System;
using Elasticsearch.Net;

namespace Slamby.Common.Exceptions
{
    public class ElasticSearchException : SlambyException
    {
        public ServerError ServerError { get; set; }

        public ElasticSearchException(string message, Exception ex, ServerError serverError) : 
            base(message, ex)
        {
            ServerError = serverError;
        }
    }
}
