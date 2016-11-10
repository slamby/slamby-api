using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ISecretManager))]
    public class SecretManager : ISecretManager
    {
        public const int SecretMinLength = 8;
        public const int SecretMaxLength = 32;
        
        private string ApiSecret = string.Empty;
        private string SecretFilename = string.Empty;

        public bool IsSet() => !string.IsNullOrWhiteSpace(ApiSecret);

        readonly IDataProtector protector;

        public SecretManager(SiteConfig siteConfig, IDataProtectionProvider provider)
        {
            this.SecretFilename = Path.Combine(siteConfig.Directory.Sys, ".secret");
            this.protector = provider.CreateProtector("SecretManager");

            Load();
        }

        public void Load()
        {
            var secret = Read();
            if (!string.IsNullOrWhiteSpace(secret))
            {
                secret = protector.Unprotect(secret);
            }
            this.ApiSecret = secret;
        }

        private string Read()
        {
            if (!File.Exists(SecretFilename))
            {
                return string.Empty;
            }

            return File.ReadLines(SecretFilename).FirstOrDefault() ?? string.Empty;
        }

        public void Change(string secret)
        {
            var encryptedSecret = protector.Protect(secret);
            File.WriteAllText(SecretFilename, encryptedSecret, Encoding.UTF8);
            this.ApiSecret = secret;
        }

        public bool IsMatch(string text)
        {
            return string.Equals(ApiSecret, text, StringComparison.Ordinal);
        }

        public Result Validate(string secret)
        {
            if (string.IsNullOrEmpty(secret) ||
                secret.Length < SecretMinLength ||
                secret.Length > SecretMaxLength)
            {
                return Result.Fail(string.Format(GlobalResources.SecretMustBeAtLeast_0_CharactersLong, SecretMinLength));
            }

            return Result.Ok();
        }
    }
}
