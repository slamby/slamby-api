using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using Slamby.Common.Services.Interfaces;
using Slamby.Common.Services;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ISecretManager))]
    public class SecretManager : ISecretManager
    {
        public const int SecretMinLength = 6;
        public const int SecretMaxLength = 32;

        IStore secretStore;

        string ApiSecret = string.Empty;

        public SecretManager(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider)
        {
            var protector = provider.CreateProtector("SecretManager");
            var path = Path.Combine(siteConfig.Value.Directory.Sys, ".secret");
            var fileStore = new FileStore(path);

            secretStore = new SecureStoreDecorator(fileStore, protector);
            
            Load();
        }

        public void Load()
        {
            ApiSecret = secretStore.Read();
        }

        public void Change(string secret)
        {
            secretStore.Write(secret);
            ApiSecret = secret;
        }

        public bool IsSet() => !string.IsNullOrWhiteSpace(ApiSecret);

        public bool IsMatch(string text) => string.Equals(ApiSecret, text, StringComparison.Ordinal);

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
