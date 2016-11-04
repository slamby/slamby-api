using Microsoft.AspNetCore.Mvc;

namespace Slamby.Common.Helpers
{
    public class HttpStatusCodeWithObjectResult : ObjectResult
    {
        public HttpStatusCodeWithObjectResult(int statusCode, object value) : base(value)
        {
            StatusCode = statusCode;
        }
    }
}
