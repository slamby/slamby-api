using Microsoft.AspNetCore.Http;
using Slamby.API.Helpers;
using Slamby.Common.Config;
using Slamby.Common.DI;

namespace Slamby.API.Services
{
    [ScopedDependency]
    public class UrlProvider
    {
        readonly IHttpContextAccessor contextAccessor;
        readonly SiteConfig siteConfig;

        public UrlProvider(IHttpContextAccessor contextAccessor, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.contextAccessor = contextAccessor;
        }

        public string GetHostUrl()
        {
            return HostUrlHelper.GetHostUrl(contextAccessor.HttpContext.Request, siteConfig.BaseUrlPrefix);
        }
    }
}
