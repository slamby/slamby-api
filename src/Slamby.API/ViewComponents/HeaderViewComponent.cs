using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Slamby.Common.Config;

namespace Slamby.API.ViewComponents
{
    public class HeaderViewComponent : ViewComponent
    {
        readonly SiteConfig siteConfig;

        public HeaderViewComponent(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View("Default", siteConfig.Version);
        }
    }
}
