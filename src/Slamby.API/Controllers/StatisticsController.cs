using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Swashbuckle.SwaggerGen.Annotations;
using Microsoft.AspNetCore.Http;
using Slamby.SDK.Net.Models;
using Slamby.API.Helpers.Services;
using System.Globalization;
using Slamby.Common.Helpers;
using Slamby.API.Resources;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Statistics")]
    [SwaggerResponseRemoveDefaults]
    public class StatisticsController : BaseController
    {
        [HttpGet("{year?}/{month?}")]
        [SwaggerOperation("GetStatistics")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(StatisticsWrapper))]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult Get([FromServices]StatisticsRedisHandler statsRedisHandler, int year = -1, int month = -1)
        {
            DateTime dateTime;
            if (year > -1 && (year < 1000 || year > 9999))
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(GlobalResources.TheArgument_0_MustBeInFormat_1, "year", "yyyy"));
            if (month > -1 && (month < 1 || month > 12))
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(GlobalResources.TheArgument_0_MustBeInFormat_1, "month", "MM"));

            var yearMonth = string.Format("{0}{1}", year > -1 ? year.ToString() : string.Empty, month > -1 ? $"-{month.ToString("D2")}" : string.Empty);

            var requestsDic = statsRedisHandler.GetRequests(yearMonth);
            var stats = new StatisticsWrapper
            {
                Sum = statsRedisHandler.GetAllRequestCount(),
                Statistics = requestsDic.ToDictionary(rd =>
                            rd.Key,
                            rd => new Statistics
                            {
                                Sum = rd.Value.Sum(r => r.Value),
                                Actions = rd.Value.Select(r => new SDK.Net.Models.Action { Name = r.Key, Count = r.Value }).ToList()
                            })
            };
            return Ok(stats);
        }
    }
}
