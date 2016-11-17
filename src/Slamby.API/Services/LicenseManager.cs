using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.Elastic.Factories;
using Slamby.License.Core;
using Slamby.License.Core.Validation;
using Slamby.License.Core.Models;
using System.Threading;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ILicenseManager))]
    public class LicenseManager : ILicenseManager
    {
        readonly Uri LicenseServerUri = new Uri("https://license.slamby.com/");

        /// <summary>
        /// Instance Id (persistent)
        /// Created at first startup
        /// </summary>
        public string InstanceId { get; private set; }

        string publicKey = string.Empty;

        /// <summary>
        /// Startup time
        /// </summary>
        public DateTime StartupTime { get; } = DateTime.UtcNow;

        readonly IStore applicationIdStore;
        readonly IStore licenseStore;
        readonly ElasticClientFactory clientFactory;
        private CancellationToken StopToken;
        private License.Core.License ApplicationLicense;

        public LicenseManager(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider, IHostingEnvironment env, ElasticClientFactory clientFactory,
            IApplicationLifetime applicationLifetime)
        {
            this.clientFactory = clientFactory;

            var protector = provider.CreateProtector("ServerIdManager");
            var appIdPath = Path.Combine(siteConfig.Value.Directory.Sys, ".appid");
            var licensePath = Path.Combine(siteConfig.Value.Directory.Sys, ".license");

            applicationIdStore = new FileStore(appIdPath);
            licenseStore = new FileStore(licensePath);

            var publicKeyStore = new FileStore(Path.Combine(env.WebRootPath, "publicKey"));
            publicKey = publicKeyStore.Read();

            var licenseKeyXml = licenseStore.Read();
            if (!string.IsNullOrEmpty(licenseKeyXml))
            {
                ApplicationLicense = License.Core.License.Load(licenseKeyXml);
            }

            StopToken = applicationLifetime.ApplicationStopping;
            StartBackgroundValidator();
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
                var validaton = await ValidateAsync(licenseStore.Read());

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

            InstanceId = appId;
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

            licenseKey = licenseKey.Trim();

            var licenseKeyXml = licenseKey.IsBase64()
                ? UnwrapBase64(licenseKey)
                : licenseKey;

            // broken base64 string and not XML
            if (licenseKeyXml.IndexOf('<') == -1)
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

            return Enumerable.Empty<ValidationFailure>();
        }

        private string WrapBase64(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        private string UnwrapBase64(string base64)
        {
            base64 = base64.Trim();
            base64 = Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));

            return base64;
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

            var checkResponse = await RequestCheck(xmlText);

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
                xmlText = UnwrapBase64(checkResponse.License);

                checkResponse = await RequestCheck(xmlText);

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

        private async Task<CheckResponseModel> RequestCheck(string xmlText)
        {
            try
            {
                var elasticName = clientFactory.GetClient().RootNodeInfo()?.Name ?? string.Empty;

                using (var client = CreateHttpClient())
                {
                    var model = new CheckRequestModel()
                    {
                        Id = Guid.Parse(InstanceId),
                        License = WrapBase64(xmlText),
                        Details = new CheckDetailsModel()
                        {
                            LaunchTime = StartupTime,
                            Cores = Environment.ProcessorCount,
                            ElasticName = elasticName
                        }
                    };

                    var json = JsonConvert.SerializeObject(model);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("api/check", content);

                    if (response.StatusCode != System.Net.HttpStatusCode.BadRequest &&
                        response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var checkResponse = JsonConvert.DeserializeObject<CheckResponseModel>(responseBody);

                    return checkResponse;
                }
            }
            catch (Exception)
            {
                return null;
            }
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
                        .And().Id(InstanceId)
                        .And().ExpirationDate()
                        .And().Cores(Environment.ProcessorCount)
                        .AssertValidLicense()
                        .ToList();
                }
                else
                {
                    validationFailures = license.Validate()
                        .Signature(publicKey)
                        .And().Id(InstanceId)
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
            try
            {
                using (var client = CreateHttpClient())
                {
                    var model = new CreateOpenSourceModel()
                    {
                        Id = Guid.Parse(InstanceId),
                        Email = email
                    };

                    var json = JsonConvert.SerializeObject(model);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("api/create", content);

                    if (response.StatusCode != System.Net.HttpStatusCode.BadRequest &&
                        response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var createResponse = JsonConvert.DeserializeObject<CreateResponseModel>(responseBody);

                    return createResponse;
                }
            }
            catch
            {
                return null;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            client.BaseAddress = LicenseServerUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }
    }
}
