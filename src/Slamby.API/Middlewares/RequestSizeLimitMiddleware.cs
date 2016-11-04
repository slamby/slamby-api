using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using static Slamby.API.Resources.GlobalResources;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Slamby.Common.Services;

namespace Slamby.API.Middlewares
{
    public class RequestSizeLimitMiddleware
    {

        private readonly RequestDelegate _next;

        public RequestSizeLimitMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, [FromServices]MachineResourceService resourceService)
        {
            if (resourceService.MaxRequestSize > 0 && context.Request.ContentLength > resourceService.MaxRequestSize)
            {
                string response = Newtonsoft.Json.JsonConvert.SerializeObject(new SDK.Net.Models.ErrorsModel { Errors = new List<string> { string.Format(Request_Is_Too_Large_0, resourceService.MaxRequestSize) } });
                context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = response.Length;
                await context.Response.WriteAsync(response);

                return;
            }
            

            await _next.Invoke(context);
        }
    }
}
