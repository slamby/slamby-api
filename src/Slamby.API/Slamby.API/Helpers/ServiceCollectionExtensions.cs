using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Slamby.API.Helpers
{
    public static class ServiceCollectionExtensions
    {
        public static void Dump(this IServiceCollection services)
        {
            var registeredServices = services
                .Where(w => w.ServiceType.FullName.StartsWith("Slamby.", StringComparison.Ordinal))
                .OrderBy(o => o.ServiceType.FullName)
                .Select(s => new
                {
                    Lifetime = s.Lifetime,
                    ServiceType = s.ServiceType.FullName,
                    ImplType = s.ImplementationType?.FullName,
                    ImplFactory = s.ImplementationFactory != null ? "[Factory]" : null,
                    ImplInstance = s.ImplementationInstance != null ? "[Instane]" : null
                })
                .Select(s => new { s.Lifetime, s.ServiceType, Impl = s.ImplType ?? s.ImplFactory ?? s.ImplInstance })
                .Select(s => $"[{s.Lifetime,-9}] {s.ServiceType,-60} -> {s.Impl}")
                .ToList();

            Log.Logger.Information("\n\rRegistered Services:\n\r" + string.Join("\n\r", registeredServices));
        }
    }
}
