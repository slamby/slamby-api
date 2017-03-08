using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Slamby.API.Services.Interfaces;
using Slamby.Common.DI;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Slamby.API.Filters
{
    [TransientDependency]
    public class ServiceBusyFilter : ActionFilterAttribute
    {
        readonly IGlobalStoreManager GlobalStore;
        private const string idProperty = "id";

        public ServiceBusyFilter([FromServices]IGlobalStoreManager globalStore)
        {
            GlobalStore = globalStore;
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ActionArguments.ContainsKey(idProperty)) base.OnActionExecuting(context);
            var id = context.ActionArguments[idProperty].ToString();
            GlobalStore.ServiceAliases.AddBusy(id);
            base.OnActionExecuting(context);
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.RouteData.Values.ContainsKey(idProperty))
            {
                var id = context.RouteData.Values[idProperty].ToString();
                GlobalStore.ServiceAliases.RemoveBusy(id);
            }
            base.OnActionExecuted(context);
        }
    }
}
