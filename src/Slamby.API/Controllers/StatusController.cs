using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.Common.Services;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Status")]
    [SwaggerResponseRemoveDefaults]
    public class StatusController : BaseController
    {
        [HttpGet]
        [SwaggerOperation("GetStatus")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Status))]
        public IActionResult Get([FromServices]MachineResourceService resourceService)
        {
            return Ok(resourceService.Status);
        }
    }
}
