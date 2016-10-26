using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Slamby.API.Resources;
using Slamby.Common.Config;
using Slamby.SDK.Net;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Filters
{
    public class SdkVersionFilter : ActionFilterAttribute
    {
        readonly SiteConfig siteConfig;

        public SdkVersionFilter(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Request.Headers.ContainsKey(Constants.SdkVersionHeader))
            {
                var sdkVersion = TakeMajorMinor(context.HttpContext.Request.Headers[Constants.SdkVersionHeader].ToString());
                var apiVersion = TakeMajorMinor(siteConfig.Version);

                if (!string.Equals(sdkVersion, apiVersion))
                {
                    context.Result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                        ErrorsModel.Create(string.Format(GlobalResources.SdkApiVersionMismatch, sdkVersion, apiVersion))
                    );

                    return;
                }
            }

            base.OnActionExecuting(context);
        }

        string TakeMajorMinor(string str)
        {
            var idx = NthIndexOf(str, '.', 2);
            return (idx == -1) ? str : str.Substring(0, idx);
        }

        int NthIndexOf(string text, char separator, int occurences)
        {
            var takeCount = text.TakeWhile(x => (occurences -= (x == separator ? 1 : 0)) > 0).Count();
            return takeCount == text.Length ? -1 : takeCount;
        }
    }
}
