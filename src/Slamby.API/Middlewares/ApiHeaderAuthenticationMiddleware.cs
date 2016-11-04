using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Slamby.Common.Config;
using Slamby.SDK.Net;

namespace Slamby.API.Middlewares
{
    public class ApiHeaderAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        
        public ApiHeaderAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, SiteConfig siteConfig)
        {
            // Validate Authentication header only on /api/...
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next.Invoke(context);
                return;
            }

            if (!context.Request.Headers.Keys.Contains(SDK.Net.Constants.AuthorizationHeader))
            {
                context.Response.StatusCode = 401;
                return;
            }

            var authorizationValues = context.Request
                .Headers[Constants.AuthorizationHeader]
                .ToString()
                .Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

            if (authorizationValues.Length < 2 ||
                !string.Equals(authorizationValues[0], Constants.AuthorizationMethodSlamby, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(authorizationValues[1], siteConfig.ApiSecret, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 401;
                return;
            }

            await _next.Invoke(context);
        }
    }
}
