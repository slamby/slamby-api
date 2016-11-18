using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.License.Core;
using Slamby.License.Core.Validation;
using Slamby.License.Core.Models;
using System.Threading;
using Slamby.API.Services.Interfaces;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ILicenseManager))]
    public class LicenseManager : ILicenseManager
    {
        /// <summary>
        /// Instance Id (persistent)
        /// Created at first startup
        /// </summary>
        public Guid InstanceId { get; private set; }

        string publicKey = string.Empty;

        /// <summary>
        /// Startup time
        /// </summary>
        public DateTime StartupTime { get; } = DateTime.UtcNow;

        IStore applicationIdStore;
        IStore licenseStore;

        private CancellationToken StopToken;
        public License.Core.License ApplicationLicense { get; private set; }
        public IEnumerable<ValidationFailure> ApplicationLicenseValidation { get; private set; } = new List<ValidationFailure>();

        readonly ILicenseServerClient licenseServerClient;

        public LicenseManager(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider, ILicenseServerClient licenseServerClient,
            IHostingEnvironment env, IApplicationLifetime applicationLifetime)
        {
            this.licenseServerClient = licenseServerClient;
            this.StopToken = applicationLifetime.ApplicationStopping;

            InitStores(siteConfig, provider);
            LoadPublicKey(env);
            LoadLicenseKey();

            StartBackgroundValidator();
        }

        private void InitStores(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider)
        {
            var protector = provider.CreateProtector("ServerIdManager");
            var appIdPath = Path.Combine(siteConfig.Value.Directory.Sys, ".appid");
            var licensePath = Path.Combine(siteConfig.Value.Directory.Sys, ".license");

            applicationIdStore = new FileStore(appIdPath);
            licenseStore = new FileStore(licensePath);
        }

        private void LoadPublicKey(IHostingEnvironment env)
        {
            var publicKeyStore = new FileStore(Path.Combine(env.WebRootPath, "publicKey"));
            publicKey = publicKeyStore.Read();
        }

        private void LoadLicenseKey()
        {
            var licenseKeyXml = licenseStore.Read();
            if (!string.IsNullOrEmpty(licenseKeyXml))
            {
                ApplicationLicense = License.Core.License.Load(licenseKeyXml);
            }
        }

        private void StartBackgroundValidator()
        {
            new TaskFactory()
                .StartNew(
                    async () => { await ValidatePeriodically(); },
                    TaskCreationOptions.LongRunning);
        }

        private async Task ValidatePeriodically()
        {
            while (!StopToken.IsCancellationRequested)
            {
                ApplicationLicenseValidation = await ValidateAsync(licenseStore.Read());

                try
                {
                    Task.Delay(TimeSpan.FromHours(1))
                        .Wait(StopToken);
                }
                catch (OperationCanceledException)
                {
                    // If token cancelled OperationCanceledException is thrown, but it is expected exception
                    break;
                }
            }
        }

        public void EnsureAppIdCreated()
        {
            var appId = applicationIdStore.Read();

            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = Guid.NewGuid().ToString();
                applicationIdStore.Write(appId);
            }

            InstanceId = Guid.Parse(appId);
        }

        public string Get()
        {
            return licenseStore.Read() ?? string.Empty;
        }

        public async Task<IEnumerable<ValidationFailure>> SaveAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                throw new ArgumentNullException(nameof(licenseKey));
            }

            licenseKey = licenseKey.CleanBase64();

            var licenseKeyXml = licenseKey.IsBase64()
                ? licenseKey.FromBase64()
                : licenseKey;

            // broken base64 string and not XML
            if (licenseKeyXml.IndexOf('<') == -1 && licenseKeyXml.ContainsBase64Chars())
            {
                return new List<ValidationFailure>() { new ValidationFailure() { Message = "The entered key is partial. Please paste full license key." } };
            }

            var validation = await ValidateAsync(licenseKeyXml);
            if (validation.Any())
            {
                return validation;
            }

            licenseStore.Write(licenseKeyXml);

            ApplicationLicense = License.Core.License.Load(licenseKeyXml);
            ApplicationLicenseValidation = validation;

            return Enumerable.Empty<ValidationFailure>();
        }

        public async Task<IEnumerable<ValidationFailure>> ValidateAsync(string licenseKey)
        {
            var validationOnline = await ValidateOnlineAsync(licenseKey);

            // Could not validate online, let's validate offline
            if (validationOnline.Item1 == false)
            {
                var validationOffline = ValidateOffline(licenseKey);
                if (validationOffline.Any())
                {
                    return validationOffline;
                }
            }
            else
            {
                if (validationOnline.Item2.Any())
                {
                    return validationOnline.Item2;
                }
            }

            return Enumerable.Empty<ValidationFailure>();
        }

        private async Task<Tuple<bool, IEnumerable<ValidationFailure>>> ValidateOnlineAsync(string xmlText)
        {
            var unknownFailure = new List<ValidationFailure>() { new ValidationFailure() { Message = "Failed to validate Online" } };

            var checkResponse = await licenseServerClient.RequestCheck(xmlText, InstanceId, StartupTime);

            if (checkResponse == null)
            {
                return new Tuple<bool, IEnumerable<ValidationFailure>>(false, unknownFailure);
            }

            if (checkResponse.IsValid)
            {
                return new Tuple<bool, IEnumerable<ValidationFailure>>(true, Enumerable.Empty<ValidationFailure>());
            }

            // we got new refreshed license
            if (!string.IsNullOrEmpty(checkResponse.License))
            {
                xmlText = checkResponse.License.FromBase64();

                checkResponse = await licenseServerClient.RequestCheck(xmlText, InstanceId, StartupTime);

                if (checkResponse == null)
                {
                    return new Tuple<bool, IEnumerable<ValidationFailure>>(false, unknownFailure);
                }

                if (checkResponse.IsValid)
                {
                    return new Tuple<bool, IEnumerable<ValidationFailure>>(true, Enumerable.Empty<ValidationFailure>());
                }
            }

            return new Tuple<bool, IEnumerable<ValidationFailure>>(true, checkResponse.Failures ?? unknownFailure);
        }

        private IEnumerable<ValidationFailure> ValidateOffline(string xmlText)
        {
            try
            {
                var license = License.Core.License.Load(xmlText);
                var validationFailures = Enumerable.Empty<ValidationFailure>();

                if (license.Type == LicenseType.Commercial)
                {
                    validationFailures = license.Validate()
                        .Signature(publicKey)
                        .And().Id(InstanceId.ToString())
                        .And().ExpirationDate()
                        .And().Cores(Environment.ProcessorCount)
                        .AssertValidLicense()
                        .ToList();
                }
                else
                {
                    validationFailures = license.Validate()
                        .Signature(publicKey)
                        .And().Id(InstanceId.ToString())
                        .AssertValidLicense()
                        .ToList();
                }

                return validationFailures;
            }
            catch (XmlException ex)
            {
                return new List<ValidationFailure>() { new ValidationFailure() { Message = "Invaid XML format", HowToResolve = ex.Message } };
            }
        }

        public async Task<CreateResponseModel> Create(string email)
        {
            return await licenseServerClient.Create(email, InstanceId);
        }
    }
}
