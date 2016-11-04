using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Slamby.Common.Helpers;

namespace Slamby.API.Controllers
{
    public class BaseController : Controller
    {
        protected IActionResult HttpObjectResult(int statusCode, object value)
        {
            return new HttpStatusCodeWithObjectResult(statusCode, value);
        }

        /// <summary>
        /// Converts error string into ErrorsModel
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        protected IActionResult HttpErrorResult(int statusCode, string error)
        {
            return new HttpStatusCodeWithErrorResult(statusCode, error);
        }

        /// <summary>
        /// Converts error strings into ErrorsModel
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        protected IActionResult HttpErrorResult(int statusCode, IEnumerable<string> errors)
        {
            return new HttpStatusCodeWithErrorResult(statusCode, errors);
        }
    }
}
