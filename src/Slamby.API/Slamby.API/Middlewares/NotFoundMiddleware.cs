using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Newtonsoft.Json;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Middlewares
{
    public class NotFoundMiddleware
    {
        readonly RequestDelegate next;

        public NotFoundMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/")
            {
                await next.Invoke(context);
                return;
            }

            var jsonResponse = JsonConvert.SerializeObject(
                ErrorsModel.Create("Endpoint not found"),
                Formatting.Indented);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
