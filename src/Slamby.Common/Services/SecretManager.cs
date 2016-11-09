using System;
using System.IO;
using System.Linq;
using System.Text;
using Slamby.Common.Config;
using Slamby.Common.DI;

namespace Slamby.Common.Services
{
    [SingletonDependency]
    public class SecretManager
    {
        public const int SecretMinLength = 8;
        public const int SecretMaxLength = 32;

        readonly SiteConfig siteConfig;
        private string ApiSecret = string.Empty;
        public bool IsSet() => !string.IsNullOrWhiteSpace(ApiSecret);
        string SecretFilename { get { return Path.Combine(siteConfig.Directory.User, ".secret"); } }

        public SecretManager(SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            Load();
        }

        public void Load()
        {
            this.ApiSecret = Read();
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
            File.WriteAllText(SecretFilename, secret, Encoding.UTF8);
            Load();
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
