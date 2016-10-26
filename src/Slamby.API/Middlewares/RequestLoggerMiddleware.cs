using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Slamby.API.Helpers;
using Slamby.Common.Config;

namespace Slamby.API.Middlewares
{
    public class RequestLoggerMiddleware
    {
        readonly RequestDelegate next;
        readonly ILogger _logger;
        readonly SiteConfig siteConfig;

        public RequestLoggerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.next = next;
            _logger = loggerFactory.CreateLogger<RequestLoggerMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = Guid.NewGuid().ToString();
            var ignoreContent = siteConfig.RequestLogger.IgnoreContent.Any(
                path => context.Request.Path.StartsWithSegments(new PathString(path), StringComparison.OrdinalIgnoreCase)
                );

            if (ignoreContent)
            {
                await LogWithoutContent(context, requestId);
                return;
            }

            await LogWithContent(context, requestId);
        }

        private async Task LogWithContent(HttpContext context, string requestId)
        {
            // Replace Request Body with own MemoryStream
            var originalRequestBody = context.Request.Body;
            var requestBodyStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(requestBodyStream);

            requestBodyStream.Seek(0, SeekOrigin.Begin);
            _logger.LogTrace(RequestLoggerHelper.Format(context.Request, requestId, requestBodyStream, siteConfig.BaseUrlPrefix));
            requestBodyStream.Seek(0, SeekOrigin.Begin);

            context.Request.Body = requestBodyStream;

            // Replace Response Body with own MemoryStream
            var bodyStream = context.Response.Body;
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // Call next Middleware
            await next(context);

            context.Request.Body = originalRequestBody;

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            _logger.LogTrace(RequestLoggerHelper.Format(context.Response, requestId, responseBodyStream));
            responseBodyStream.Seek(0, SeekOrigin.Begin);

            await responseBodyStream.CopyToAsync(bodyStream);
        }

        private async Task LogWithoutContent(HttpContext context, string requestId)
        {
            _logger.LogTrace(RequestLoggerHelper.Format(context.Request, requestId, null, siteConfig.BaseUrlPrefix));

            // Call next Middleware
            await next(context);

            _logger.LogTrace(RequestLoggerHelper.Format(context.Response, requestId, null));
        }

        
    }
}
