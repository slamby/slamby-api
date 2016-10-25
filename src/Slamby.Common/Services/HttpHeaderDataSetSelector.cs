using Microsoft.AspNetCore.Http;
using Slamby.Common.DI;
using Slamby.Common.Services.Interfaces;
using static Slamby.SDK.Net.Constants;

namespace Slamby.Common.Services
{
    [TransientDependency(ServiceType = typeof(IDataSetSelector))]
    public class HttpHeaderDataSetSelector : IDataSetSelector
    {
        public string DataSetName { get; set; }

        public HttpHeaderDataSetSelector(IHttpContextAccessor contextAccessor)
        {
            string dataSetName = null;

            if (contextAccessor?.HttpContext?.Request != null && contextAccessor.HttpContext.Request.Headers.ContainsKey(DataSetHeader))
            {
                dataSetName = contextAccessor.HttpContext.Request.Headers[DataSetHeader];
            }

            DataSetName =  dataSetName;
        }
    }
}
