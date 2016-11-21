using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.Common.Helpers;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("License")]
    [SwaggerResponseRemoveDefaults]
    public class LicenseController : BaseController
    {
        readonly ILicenseManager licenseManager;

        public LicenseController(ILicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
        }

        [HttpGet]
        [SwaggerOperation("GetLicense")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(SDK.Net.Models.License))]
        public IActionResult Get()
        {
            var model = new SDK.Net.Models.License()
            {
                IsValid = false,
                Type = "Not set",
                Message = string.Empty,
                Base64 = string.Empty
            };

            if (licenseManager.ApplicationLicense != null)
            {
                model = new SDK.Net.Models.License()
                {
                    IsValid = !licenseManager.ApplicationLicenseValidation.Any(),
                    Type = licenseManager.ApplicationLicense.Type.ToString(),
                    Message = licenseManager.ApplicationLicenseValidation.Aggregate(string.Empty, (curr, next) => curr += next.Message),
                    Base64 = licenseManager.ApplicationLicense.ToString().ToBase64()
                };
            }

            return Ok(model);
        }

        [HttpPost]
        [SwaggerOperation("ChangeLicense")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public async Task<IActionResult> Post([FromBody] ChangeLicense model)
        {
            if (string.IsNullOrEmpty(model.License))
            {
                return BadRequest(ErrorsModel.Create(GlobalResources.LicenseIsEmpty));
            }

            var validation = await licenseManager.SaveAsync(model.License);
            if (validation.Any())
            {
                return BadRequest(ErrorsModel.Create(validation.Select(v => $"{v.Message} {v.HowToResolve}")));
            }

            return Ok();
        }
    }
}
