using System;
using System.Linq;
using Microsoft.AspNet.Http;

namespace Slamby.Common.Helpers
{
    public static class HttpContextHelper
    {
        public static bool IsRequestHeaderContains(this HttpContext context, string header, string value)
        {
            var requestHeader = context.Request.Headers[header];
            return requestHeader.Any(item => item.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) != -1);
        }
    }
}
