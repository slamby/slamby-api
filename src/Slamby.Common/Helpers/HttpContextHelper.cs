using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

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
