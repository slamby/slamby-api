using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.AspNetCore.Hosting;

namespace Slamby.Common.DI
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureAttributedDependencies(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var env = serviceProvider.GetService<IHostingEnvironment>();
            Scanner.RegisterAttributedDependencies(services, env.ApplicationName);
            return services;
        }
    }
}
