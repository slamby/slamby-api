using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Slamby.Common.Config;
using Slamby.SDK.Net;

namespace Slamby.API.Middlewares
{
    public class ApiHeaderVersionMiddleware
    {
        private readonly RequestDelegate _next;
        
        public ApiHeaderVersionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, SiteConfig siteConfig)
        {
            if (context.Response.Headers.ContainsKey(Constants.ApiVersionHeader))
            {
                context.Response.Headers.Remove(Constants.ApiVersionHeader);
            }

            context.Response.Headers.Add(Constants.ApiVersionHeader, siteConfig.Version);

            await _next.Invoke(context);
        }
    }
}
