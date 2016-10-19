using Elasticsearch.Net;
using System;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static System.Net.DecompressionMethods;

namespace Slamby.Common.SlambyConnection
{
    internal class WebProxy : IWebProxy
    {
        private readonly Uri _uri;

        public WebProxy(Uri uri) { _uri = uri; }

        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination) => _uri;

        public bool IsBypassed(Uri host) => host.IsLoopback;
    }

    public class SlambyHttpConnection : IConnection
    {
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<int, HttpClient> _clients = new ConcurrentDictionary<int, HttpClient>();

        private string DefaultContentType => "application/json";

        public SlambyHttpConnection() { }

        private HttpClient GetClient(RequestData requestData)
        {
            var hashCode = requestData.GetHashCode();
            HttpClient client;
            if (this._clients.TryGetValue(hashCode, out client)) return client;
            lock (_lock)
            {
                if (this._clients.TryGetValue(hashCode, out client)) return client;

                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = requestData.HttpCompression ? GZip | Deflate : None
                };

                if (!string.IsNullOrEmpty(requestData.ProxyAddress))
                {
                    var uri = new Uri(requestData.ProxyAddress);
                    var proxy = new WebProxy(uri);
                    var credentials = new NetworkCredential(requestData.ProxyUsername, requestData.ProxyPassword);
                    proxy.Credentials = credentials;
                    handler.Proxy = proxy;
                }

                if (requestData.DisableAutomaticProxyDetection)
                    handler.Proxy = null;

                client = new HttpClient(handler, false)
                {
                    Timeout = requestData.RequestTimeout
                };

                client.DefaultRequestHeaders.ExpectContinue = false;

                //TODO add headers
                //client.DefaultRequestHeaders =
                this._clients.TryAdd(hashCode, client);
                return client;
            }

        }

        public virtual ElasticsearchResponse<TReturn> Request<TReturn>(RequestData requestData) where TReturn : class
        {
            var client = this.GetClient(requestData);
            var builder = new ResponseBuilder<TReturn>(requestData);
            HttpRequestMessage requestMessage = null;
            try
            {
                requestMessage = CreateHttpRequestMessage(requestData);
                var response = client.SendAsync(requestMessage, requestData.CancellationToken).GetAwaiter().GetResult();
                builder.StatusCode = (int)response.StatusCode;

                if (response.Content != null)
                    builder.Stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                builder.Exception = e;
            }
            finally
            {
                if (requestMessage != null && requestMessage.Content != null)
                {
                    if (requestMessage.Content is SlambyStreamContent) ((SlambyStreamContent)requestMessage.Content).DisposeManually(true);
                    if (requestMessage.Content is SlambyByteArrayContent) ((SlambyByteArrayContent)requestMessage.Content).DisposeManually(true);
                }
            }

            return builder.ToResponse();
        }

        public virtual async Task<ElasticsearchResponse<TReturn>> RequestAsync<TReturn>(RequestData requestData) where TReturn : class
        {
            var client = this.GetClient(requestData);
            var builder = new ResponseBuilder<TReturn>(requestData);
            HttpRequestMessage requestMessage = null;
            try
            {
                requestMessage = CreateHttpRequestMessage(requestData);
                var response = await client.SendAsync(requestMessage, requestData.CancellationToken).ConfigureAwait(false);
                builder.StatusCode = (int)response.StatusCode;

                if (response.Content != null)
                    builder.Stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                builder.Exception = e;
            }
            finally
            {
                if (requestMessage != null && requestMessage.Content != null)
                {
                    if (requestMessage.Content is SlambyStreamContent) ((SlambyStreamContent)requestMessage.Content).DisposeManually(true);
                    if (requestMessage.Content is SlambyByteArrayContent) ((SlambyByteArrayContent)requestMessage.Content).DisposeManually(true);
                }
            }

            return await builder.ToResponseAsync().ConfigureAwait(false);
        }

        private static HttpRequestMessage CreateHttpRequestMessage(RequestData requestData)
        {
            var method = ConvertHttpMethod(requestData.Method);
            var requestMessage = new HttpRequestMessage(method, requestData.Uri);

            foreach (string key in requestData.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(key, requestData.Headers.GetValues(key));
            }

            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(requestData.ContentType));

            string userInfo = null;
            if (!string.IsNullOrEmpty(requestData.Uri.UserInfo))
                userInfo = Uri.UnescapeDataString(requestData.Uri.UserInfo);
            else if (requestData.BasicAuthorizationCredentials != null)
                userInfo = requestData.BasicAuthorizationCredentials.ToString();
            if (!string.IsNullOrEmpty(userInfo))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(userInfo));
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            var data = requestData.PostData;

            if (data != null)
            {
                var stream = requestData.MemoryStreamFactory.Create();

                if (requestData.HttpCompression)
                {
                    using (var zipStream = new GZipStream(stream, CompressionMode.Compress))
                        data.Write(zipStream, requestData.ConnectionSettings);

                    requestMessage.Headers.Add("Content-Encoding", "gzip");
                    requestMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                    requestMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                }
                else
                    data.Write(stream, requestData.ConnectionSettings);

                stream.Position = 0;
                requestMessage.Content = new SlambyStreamContent(stream);
            }
            else
            {
                // Set content in order to set a Content-Type header.
                // Content gets diposed so can't be shared instance
                requestMessage.Content = new SlambyByteArrayContent(new byte[0]);
            }
            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(requestData.ContentType);
            return requestMessage;
        }

        private static System.Net.Http.HttpMethod ConvertHttpMethod(Elasticsearch.Net.HttpMethod httpMethod)
        {
            switch (httpMethod)
            {
                case Elasticsearch.Net.HttpMethod.GET: return System.Net.Http.HttpMethod.Get;
                case Elasticsearch.Net.HttpMethod.POST: return System.Net.Http.HttpMethod.Post;
                case Elasticsearch.Net.HttpMethod.PUT: return System.Net.Http.HttpMethod.Put;
                case Elasticsearch.Net.HttpMethod.DELETE: return System.Net.Http.HttpMethod.Delete;
                case Elasticsearch.Net.HttpMethod.HEAD: return System.Net.Http.HttpMethod.Head;
                default:
                    throw new ArgumentException("Invalid value for HttpMethod", nameof(httpMethod));
            }
        }

        void IDisposable.Dispose() => this.DisposeManagedResources();

        protected virtual void DisposeManagedResources()
        {
            foreach (var c in _clients)
                c.Value.Dispose();
        }
    }
}
