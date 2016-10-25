using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Slamby.Common.DI
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureAttributedDependencies(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var applicationEnvironment = serviceProvider.GetService<ApplicationEnvironment>();
            Scanner.RegisterAttributedDependencies(services, applicationEnvironment.ApplicationName);
            return services;
        }
    }
}
