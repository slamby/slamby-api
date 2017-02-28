using Microsoft.AspNetCore.Mvc.Filters;
using Slamby.API.Helpers;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
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
        readonly IGlobalStoreManager globalStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottleActionFilter"/> class.
        /// </summary>
        public ThrottleActionFilter(ThrottleService throttleService, SiteConfig siteConfig, IGlobalStoreManager globalStore)
        {
            this.siteConfig = siteConfig;
            this.throttleService = throttleService;
            this.globalStore = globalStore;
        }

        /// <summary>
        /// Called while an action is being executed.
        /// </summary>
        /// <param name="context"><see cref="ActionExecutingContext"/> instance.</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            var endpoint = $"{context.RouteData?.Values?["controller"]}/{context.RouteData?.Values?["action"]}";
            throttleService.SaveRequest(globalStore.InstanceId, endpoint);
        }
    }
}
