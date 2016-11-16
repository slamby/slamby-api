using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Slamby.API.Models;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.License.Core.Validation;

namespace Slamby.API.Controllers
{
    [Route("[controller]")]
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
                    ModelState.AddModelError("", item.HowToResolve);
                }
            }

            if (secretManager.IsSet())
            {
                return View("Completed");
            }

            return View("Index", model);
        }

        private SetupModel GetModel()
        {
            return new SetupModel()
            {
                ApplicationId = licenseManager.ApplicationId,
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
    }
}
