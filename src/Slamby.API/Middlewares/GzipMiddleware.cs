using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Slamby.Common.Helpers;

namespace Slamby.API.Middlewares
{
    /// <summary>
    /// Handles gzipped compressed request (Content-Encoding: gzip) body stream and compress response if gzip is available (Accept-Encoding: gzip).
    /// </summary>
    public class GzipMiddleware
    {
        private readonly RequestDelegate _next;

        public GzipMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Swagger UI replaces stream with a CanRead=false MemoryStream, so we cannot gzip that stream
            if (!context.IsRequestHeaderContains(HeaderNames.AcceptEncoding, "gzip") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments(Common.Constants.FilesPath) ||
                context.Request.Path.StartsWithSegments(Common.Constants.AssetsPath))
            {
                await _next(context);
                return;
            }

            if (context.IsRequestHeaderContains(HeaderNames.ContentEncoding, "gzip"))
            {
                var requestStream = new MemoryStream();

                using (var decompressStream = new GZipStream(context.Request.Body, CompressionMode.Decompress))
                {
                    await decompressStream.CopyToAsync(requestStream);   
                }

                requestStream.Position = 0;
                context.Request.Body = requestStream;
            }

            using (var responseStream = new MemoryStream())
            {
                var originalStream = context.Response.Body;
                context.Response.Body = responseStream;

                await _next(context);

                if (!context.Response.Body.CanRead ||
                    context.Response.Body.Length == 0)
                {
                    return;
                }

                context.Response.Headers.Add(HeaderNames.ContentEncoding, new string[] { "gzip" });
                responseStream.Seek(0, SeekOrigin.Begin);

                using (var compressStream = new GZipStream(originalStream, CompressionLevel.Optimal))
                {    
                    await responseStream.CopyToAsync(compressStream);
                }
            }
        }
    }
}
