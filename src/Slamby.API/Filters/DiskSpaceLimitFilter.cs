using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using System;
using System.Collections.Generic;
using static Slamby.API.Resources.GlobalResources;

namespace Slamby.API.Filters
{
    [TransientDependency]
    public class DiskSpaceLimitFilter : ActionFilterAttribute
    {
        readonly SiteConfig siteConfig;
        readonly MachineResourceService machineResource;

        public DiskSpaceLimitFilter([FromServices]MachineResourceService machineResource, [FromServices]SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.machineResource = machineResource;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (machineResource.Status.AvailableFreeSpace < siteConfig.DiskSpaceLimit.MinimumMb)
            {
                var endpoint = $"{context.RouteData?.Values?["controller"]}/{context.RouteData?.Values?["action"]}";
                var responseObj = 
                    new SDK.Net.Models.ErrorsModel
                    {
                        Errors = new List<string> {
                            string.Format(LowDiskSpace_01, siteConfig.DiskSpaceLimit.MinimumMb, machineResource.Status.AvailableFreeSpace) }
                    };

                context.Result = new JsonResult(responseObj) {StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status507InsufficientStorage };
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}