using System;
using Microsoft.Extensions.Configuration;

namespace Slamby.Common.Helpers
{
    public static class ConfigurationExtensions
    {
        public static T GetEnumValue<T>(this IConfigurationRoot configuration, string key, T defaultValue) where T: struct
        {
            T result;
            return Enum.TryParse(configuration[key], out result) ? result : defaultValue;
        }
    }
}
