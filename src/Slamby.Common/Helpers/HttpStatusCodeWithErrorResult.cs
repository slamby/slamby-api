using System.Collections.Generic;
using Slamby.SDK.Net.Models;

namespace Slamby.Common.Helpers
{
    public class HttpStatusCodeWithErrorResult : HttpStatusCodeWithObjectResult
    {
        public HttpStatusCodeWithErrorResult(int statusCode, string value) : 
            base(statusCode, ErrorsModel.Create(value))
        {
        }

        public HttpStatusCodeWithErrorResult(int statusCode, IEnumerable<string> values) :
            base(statusCode, ErrorsModel.Create(values))
        {
        }
    }
}
