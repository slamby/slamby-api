using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ILicenseManager))]
    public class LicenseManager : ILicenseManager
    {
        /// <summary>
        /// Installation Id (persistent)
        /// Created at first startup
        /// </summary>
        public string ApplicationId { get; private set; }

        /// <summary>
        /// Instance Id (volatile)
        /// Generated at every startup
        /// </summary>
        public string InstanceId
        {
            get
            {
                return StartupTime.ToString(CultureInfo.InvariantCulture.DateTimeFormat.RFC1123Pattern);
            }
        }

        const string LicenseServerUri = "https://license.slamby.com/";

        string publicKey = string.Empty;

        /// <summary>
        /// Startup time
        /// Used for creating InstanceId
        /// </summary>
        public DateTime StartupTime { get; } = DateTime.UtcNow;

        IStore applicationIdStore;
        IStore licenseStore;
        readonly ElasticClientFactory clientFactory;

        public LicenseManager(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider, IHostingEnvironment env, ElasticClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
            var publicKeyStore = new FileStore(Path.Combine(env.WebRootPath, "publicKey"));
            publicKey = publicKeyStore.Read();

            var protector = provider.CreateProtector("ServerIdManager");
            var appIdPath = Path.Combine(siteConfig.Value.Directory.Sys, ".appid");
            var licensePath = Path.Combine(siteConfig.Value.Directory.Sys, ".license");

            applicationIdStore = new FileStore(appIdPath);
            licenseStore = new FileStore(licensePath);
        }

        public void EnsureAppIdCreated()
        {
            var appId = applicationIdStore.Read();

            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = Guid.NewGuid().ToString();
                applicationIdStore.Write(appId);
            }

            ApplicationId = appId;
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

            var validation = await ValidateAsync(licenseKeyXml);
            if (validation.Any())
            {
                return validation;
            }

            licenseStore.Write(licenseKeyXml);

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

            try
            {
                var elasticName = clientFactory.GetClient().RootNodeInfo()?.Name ?? string.Empty;

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(LicenseServerUri);

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var model = new License.Models.CheckRequestModel()
                    {
                        Id = Guid.Parse(ApplicationId),
                        License = WrapBase64(xmlText),
                        Details = new License.Models.CheckDetailsModel()
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
                        return new Tuple<bool, IEnumerable<ValidationFailure>>(false, unknownFailure);
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var checkResponse = JsonConvert.DeserializeObject<License.Models.CheckResponseModel>(responseBody);

                    if (checkResponse.IsValid)
                    {
                        return new Tuple<bool, IEnumerable<ValidationFailure>>(true, Enumerable.Empty<ValidationFailure>());
                    }

                    return new Tuple<bool, IEnumerable<ValidationFailure>>(true, checkResponse.Failures ?? unknownFailure);
                }
            }
            catch (Exception)
            {
                return new Tuple<bool, IEnumerable<ValidationFailure>>(false, unknownFailure);
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
                        .And().Id(ApplicationId)
                        .And().ExpirationDate()
                        .And().Cores(Environment.ProcessorCount)
                        .AssertValidLicense()
                        .ToList();
                }
                else
                {
                    validationFailures = license.Validate()
                        .Signature(publicKey)
                        .And().Id(ApplicationId)
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
    }
}
