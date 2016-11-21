using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Slamby.API.ViewModels;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.License.Core.Validation;

namespace Slamby.API.Controllers
{
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SetupController : Controller
    {
        readonly ISecretManager secretManager;
        readonly ILicenseManager licenseManager;

        public SetupController(ISecretManager secretManager, ILicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
            this.secretManager = secretManager;
        }
        
        [HttpGet]
        public IActionResult Index()
        {
            var model = GetModel();

            if (TempData["ValidationFailures"] != null)
            {
                var validationFailures = JsonConvert.DeserializeObject<IEnumerable<ValidationFailure>>(TempData["ValidationFailures"].ToString());
                foreach (var item in validationFailures)
                {
                    ModelState.AddModelError("", item.Message);
                    if (!string.IsNullOrEmpty(item.HowToResolve))
                    {
                        ModelState.AddModelError("", item.HowToResolve);
                    }
                }
            }

            if (TempData["RequestLicense"] != null)
            {
                model.Alert = JsonConvert.DeserializeObject<AlertModel>(TempData["RequestLicense"].ToString());
            }

            if (secretManager.IsSet())
            {
                return View("Completed");
            }

            return View("Index", model);
        }

        [NonAction]
        private SetupModel GetModel()
        {
            return new SetupModel()
            {
                ApplicationId = licenseManager.InstanceId.ToString(),
                Secret = string.Empty,
                SecretMinLength = SecretManager.SecretMinLength,
                SecretMaxLength = SecretManager.SecretMaxLength,
                LicenseKey = licenseManager.Get()
            };
        }

        [HttpPost]
        public async Task<IActionResult> Index(SetupModel model)
        {
            var validationFailures = await licenseManager.SaveAsync(model.LicenseKey);
            if (validationFailures.Any())
            {
                TempData["ValidationFailures"] = JsonConvert.SerializeObject(validationFailures);

                return RedirectToAction("Index");
            }

            var result = secretManager.Validate(model.Secret);
            if (result.IsSuccess && !secretManager.IsSet())
            {
                secretManager.Change(model.Secret);
            }

            return RedirectToAction("Index");
        }

        [HttpPost("RequestOpenSourceLicense")]
        public async Task<IActionResult> RequestOpenSourceLicense(RequestLicenseModel model)
        {
            var licenseResponse = await licenseManager.Create(model.Email);
            AlertModel alert;

            if (licenseResponse == null)
            {
                alert = new AlertModel() { ClassName = "alert-danger", Message = "Unable to request license." };
            }
            else if (licenseResponse.Failures?.Any() == true)
            {
                var failure = licenseResponse.Failures.First();
                alert = new AlertModel() { ClassName = "alert-danger", Message = failure.Message + " " + failure.HowToResolve};
            }
            else
            {
                alert = new AlertModel() { ClassName = "alert-success", Message = $"License is sent to {model.Email}" };
            }

            if (alert != null)
            {
                TempData["RequestLicense"] = JsonConvert.SerializeObject(alert);
            }

            return RedirectToAction("Index");
        }
    }
}
