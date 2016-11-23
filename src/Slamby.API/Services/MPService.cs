using Microsoft.Extensions.Options;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Config;
using Slamby.Common.DI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    [SingletonDependency]
    public class MPService
    {
        private bool isMP = false;
        private string filePath;

        public MPService(IOptions<SiteConfig> siteConfig) {
            filePath = Path.Combine(siteConfig.Value.Directory.Sys, ".mp");
            isMP = File.Exists(filePath);
        }

        public bool IsMP()
        {
            return isMP;
        }
    }
}
