using System;
using System.Reflection;

namespace Slamby.Common.Helpers
{
    public static class VersionHelper
    {
        public static string GetFileVersion()
        {
            return Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;
        }

        public static string GetProductVersion(Type type)
        {
            return type
               .GetTypeInfo()
               .Assembly
               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
               .InformationalVersion;
        }
    }
}
