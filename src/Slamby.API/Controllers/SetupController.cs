using System;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Models;
using Slamby.Common.Config;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Slamby.API.Controllers
{
    [Route("[controller]")]
    public class SetupController : Controller
    {
        readonly SiteConfig siteConfig;
        const int SecretMinLength = 8;
        const int SecretMaxLength = 32;

        public SetupController(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
        }
        
        [HttpGet]
        public IActionResult Index()
        {
            var model = new SetupModel()
            {
                Version = siteConfig.Version,
                Secret = string.Empty,
                SecretMinLength = SecretMinLength,
                SecretMaxLength = SecretMaxLength
            };

            //if (!string.IsNullOrEmpty(siteConfig.ApiSecret))
            //{
            //    return View("Completed");
            //}
            
            return View("Index", model);
        }

        [HttpPost]
        public IActionResult Index(SetupModel model)
        {
            if (IsValidSecret(model.Secret)) //valid
            {
                //TODO: Persist
                siteConfig.ApiSecret = model.Secret;
            }

            return RedirectToAction("Index");
        }

        private bool IsValidSecret(string secret)
        {
            return !string.IsNullOrEmpty(secret) && secret.Length >= SecretMinLength && secret.Length <= SecretMaxLength;
        }
    }
}
