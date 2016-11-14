using Microsoft.AspNetCore.Mvc;
using Slamby.API.Models;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;

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
            var model = new SetupModel()
            {
                ApplicationId = licenseManager.ApplicationId,
                Secret = string.Empty,
                SecretMinLength = SecretManager.SecretMinLength,
                SecretMaxLength = SecretManager.SecretMaxLength
            };

            if (secretManager.IsSet())
            {
                return View("Completed");
            }

            return View("Index", model);
        }

        [HttpPost]
        public IActionResult Index(SetupModel model)
        {
            var result = secretManager.Validate(model.Secret);
            if (result.IsSuccess && !secretManager.IsSet())
            {
                secretManager.Change(model.Secret);
            }

            return RedirectToAction("Index");
        }
    }
}
