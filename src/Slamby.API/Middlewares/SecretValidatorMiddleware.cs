using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Slamby.API.Helpers;
using Slamby.API.Resources;
using Slamby.Common.Config;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Middlewares
{
    public class SecretValidatorMiddleware
    {
        private readonly RequestDelegate _next;

        public SecretValidatorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, [FromServices]SiteConfig siteConfig, [FromServices]ISecretManager secretManager)
        {
            if (!secretManager.IsSet() && !IsPathInWhiteList(context.Request.Path))
            {
                var hostUrl = HostUrlHelper.GetHostUrl(context.Request, siteConfig.BaseUrlPrefix);
                var model = ErrorsModel.Create(string.Format(GlobalResources.SecretIsNotSetVisit_0_ForSetup, $"{hostUrl}/setup"));
                var response = JsonConvert.SerializeObject(model);

                context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = response.Length;

                await context.Response.WriteAsync(response);

                return;
            }
            
            await _next.Invoke(context);
        }

        private bool IsPathInWhiteList(PathString path)
        {
            return path.HasValue &&
                (path.Value == "/" ||
                path.StartsWithSegments(Common.Constants.AssetsPath) ||
                path.StartsWithSegments(Common.Constants.SetupPath));
        }
    }
}
