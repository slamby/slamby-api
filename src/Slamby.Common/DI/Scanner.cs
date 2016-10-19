using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Slamby.Common.DI
{
    // http://dotnetliberty.com/index.php/2016/01/11/dependency-scanning-in-asp-net-5/
    // http://dotnetliberty.com/index.php/2016/01/19/dependency-scanning-in-asp-net-5-part-2/
    public class Scanner
    {
        private readonly ILibraryManager _libraryManager;
        private readonly AssemblyName _thisAssemblyName;
        private readonly List<string> _scannedAssembly = new List<string>();

        public Scanner(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
            _thisAssemblyName = new AssemblyName(GetType().GetTypeInfo().Assembly.FullName);
        }

        public void RegisterAllAssemblies(IServiceCollection services)
        {
            foreach (var assembly in GetAssembliesReferencingThis())
            {
                RegisterAssembly(services, assembly);
            }
        }

        public void RegisterAssembly(IServiceCollection services, AssemblyName assemblyName)
        {
            if (_scannedAssembly.Contains(assemblyName.FullName))
            {
                return;
            }

            var assembly = Assembly.Load(assemblyName);
            foreach (var type in assembly.DefinedTypes)
            {
                var dependencyAttributes = type.GetCustomAttributes<DependencyAttribute>();
                // Each dependency can be registered as various types
                foreach (var dependencyAttribute in dependencyAttributes)
                {
                    var serviceDescriptor = dependencyAttribute.BuildServiceDescriptor(type);
                    services.Add(serviceDescriptor);
                }
            }

            _scannedAssembly.Add(assemblyName.FullName);
        }

        private IEnumerable<AssemblyName> GetAssembliesReferencingThis()
        {
            var assemblies = _libraryManager.GetReferencingLibraries(_thisAssemblyName.Name)
                .SelectMany(library => library.Assemblies)
                .Concat(new[] { _thisAssemblyName }); // And add self since 'Slamby.Common' is not just for DI stuff, it can contains dependencies also

            return assemblies;
        }
    }
}
