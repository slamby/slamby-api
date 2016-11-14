using System;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;

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

        /// <summary>
        /// Startup time
        /// Used for creating InstanceId
        /// </summary>
        public DateTime StartupTime { get; } = DateTime.UtcNow;

        IStore applicationIdStore;
        IStore licenseStore;

        public LicenseManager(IOptions<SiteConfig> siteConfig, IDataProtectionProvider provider)
        {
            var protector = provider.CreateProtector("ServerIdManager");

            var appIdPath = Path.Combine(siteConfig.Value.Directory.Sys, ".appid");
            var licensePath = Path.Combine(siteConfig.Value.Directory.Sys, ".license");

            applicationIdStore = new FileStore(appIdPath);
            licenseStore = new FileStore(licensePath);
        }

        public void EnsureCreated()
        {
            var appId = applicationIdStore.Read();

            if (string.IsNullOrWhiteSpace(appId))
            {
                appId = Guid.NewGuid().ToString();
                applicationIdStore.Write(appId);
            }

            ApplicationId = appId;
        }
    }
}
