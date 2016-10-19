using System.Collections.Generic;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Filters;
using Slamby.API.Services.Interfaces;
using Slamby.SDK.Net.Models;
using static Slamby.API.Resources.GlobalResources;
using static Slamby.SDK.Net.Constants;

namespace Slamby.API.Filters
{
    public class DataSetNameFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.Request.Headers.ContainsKey(DataSetHeader))
            {
                context.Result = new BadRequestObjectResult(
                    new SDK.Net.Models.ErrorsModel
                    {
                        Errors = new List<string> { string.Format(Missing_0_Header, DataSetHeader)}
                    });
                return;
            }

            var dataSetName = context.HttpContext.Request.Headers[DataSetHeader];

            var globalStore = (IGlobalStoreManager)context.HttpContext.RequestServices.GetService(typeof(IGlobalStoreManager));
            if (!globalStore.DataSets.IsExist(dataSetName))
            {
                context.Result = new BadRequestObjectResult(ErrorsModel.Create(string.Format(DataSet_0_NotFound, dataSetName)));
                return;
            }

            context.RouteData.Values.Add(DataSetHeader, dataSetName);
            base.OnActionExecuting(context);
        }
    }
}
