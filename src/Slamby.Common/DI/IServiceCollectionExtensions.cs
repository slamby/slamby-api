using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Slamby.Common.DI
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddDependencyScanning(this IServiceCollection services)
        {
            services.AddSingleton<Scanner>();
            return services;
        }

        public static IServiceCollection Scan(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var appEnv = serviceProvider.GetService<IApplicationEnvironment>();
            var scanner = serviceProvider.GetService<Scanner>();

            scanner.RegisterAssembly(services, new AssemblyName(appEnv.ApplicationName));
            scanner.RegisterAllAssemblies(services);

            return services;
        }
    }
}
