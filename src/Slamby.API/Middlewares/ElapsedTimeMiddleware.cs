using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Slamby.API.Middlewares
{
    public class ElapsedTimeMiddleware
    {
        readonly RequestDelegate _next;

        public ElapsedTimeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var timer = Stopwatch.StartNew();

            //To add Headers AFTER everything you need to do this
            context.Response.OnStarting(state => {
                var httpContext = (HttpContext)state;
                httpContext.Response.Headers.Add("X-ElapsedTime", new[] { timer.ElapsedMilliseconds.ToString() });
                return Task.FromResult(0);
            }, context);

            //Once everything unwinds the OnStarting should get called and we can then record the elapsed time.
            await _next(context);
        }
    }
}
