using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Slamby.API.Helpers;
using Slamby.Common.Config;
using System.Threading.Tasks;

namespace Slamby.API.Middlewares
{
    public class PathBaseMiddleware
    {
        readonly RequestDelegate next;
        readonly ILogger _logger;
        readonly SiteConfig siteConfig;

        public PathBaseMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.next = next;
            _logger = loggerFactory.CreateLogger<RequestLoggerMiddleware>();
        }

        public void Invoke(HttpContext context)
        {
            context.Request.PathBase = HostUrlHelper.GetPathBase(context.Request, siteConfig.BaseUrlPrefix);
            return;
        }
    }
}
