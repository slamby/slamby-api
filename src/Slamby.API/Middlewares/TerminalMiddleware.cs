using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Slamby.API.Services;
using Slamby.Common.Config;

namespace Slamby.API.Middlewares
{
    public class TerminalMiddleware
    {
        readonly IOptions<SiteConfig> siteConfig;
        readonly ILicenseManager licenseManager;
        readonly RequestDelegate next;

        public TerminalMiddleware(RequestDelegate next, IOptions<SiteConfig> siteConfig, ILicenseManager licenseManager)
        {
            this.next = next;
            this.licenseManager = licenseManager;
            this.siteConfig = siteConfig;
        }

        public async Task Invoke(HttpContext context)
        {
            var model = new
            {
                Name = "Slamby.API",
                Version = siteConfig.Value.Version,
                InstanceId = licenseManager.InstanceId
            };

            context.Response.ContentType = "application/json";
            var response = JsonConvert.SerializeObject(model, Formatting.Indented);

            await context.Response.WriteAsync(response);
        }
    }
}
