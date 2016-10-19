using Microsoft.AspNet.Mvc.Filters;
using Slamby.API.Helpers;
using Slamby.API.Services;
using Slamby.Common.Config;

namespace Slamby.API.Filters
{
    /// <summary>
    /// This represents the filter attribute entity for global actions.
    /// </summary>
    public class ThrottleActionFilter : ActionFilterAttribute
    {
        readonly ThrottleService throttleService;
        readonly SiteConfig siteConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottleActionFilter"/> class.
        /// </summary>
        public ThrottleActionFilter(ThrottleService throttleService, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.throttleService = throttleService;
        }

        /// <summary>
        /// Called while an action is being executed.
        /// </summary>
        /// <param name="context"><see cref="ActionExecutingContext"/> instance.</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            var request = context.HttpContext.Request;
            var endpoint = context.HttpContext.Request.Path.ToString();

            throttleService.SaveRequest(HostUrlHelper.GetHostUrl(request, siteConfig.BaseUrlPrefix), endpoint);
        }
    }
}
