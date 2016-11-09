using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services.Interfaces;

namespace Slamby.Common.Services
{
    [SingletonDependency(ServiceType = typeof(ISecretManager))]
    public class SecretManager : ISecretManager
    {
        public const int SecretMinLength = 8;
        public const int SecretMaxLength = 32;

        readonly SiteConfig siteConfig;
        private string ApiSecret = string.Empty;
        public bool IsSet() => !string.IsNullOrWhiteSpace(ApiSecret);
        string SecretFilename { get { return Path.Combine(siteConfig.Directory.User, ".secret"); } }

        readonly IDataProtector protector;

        public SecretManager(SiteConfig siteConfig, IDataProtectionProvider provider)
        {
            this.protector = provider.CreateProtector("SecretManager");
            this.siteConfig = siteConfig;
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

        public bool Validate(string secret)
        {
            return !string.IsNullOrEmpty(secret) && 
                secret.Length >= SecretMinLength && 
                secret.Length <= SecretMaxLength;
        }
    }
}
