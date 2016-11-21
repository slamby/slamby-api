using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]/ChangeSecret")]
    [SwaggerGroup("Maintenance")]
    [SwaggerResponseRemoveDefaults]
    public class MaintenanceController : BaseController
    {
        [HttpPost()]
        [SwaggerOperation("ChangeSecret")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable, "", typeof(ErrorsModel))]
        public IActionResult ChangeSecret([FromBody]ChangeSecret secret, [FromServices]ISecretManager secretManager)
        {
            var result = secretManager.Validate(secret.Secret);
            if (result.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status406NotAcceptable, result.Error);
            }

            secretManager.Change(secret.Secret);

            return Ok();
        }
    }
}
