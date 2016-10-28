using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Slamby.Common.DI
{
    /// <summary>
    /// Discovers assemblies that are part of the application using the DependencyContext.
    /// </summary>
    /// <remarks>
    /// Source: https://github.com/aspnet/Mvc/blob/760c8f38678118734399c58c2dac981ea6e47046/src/Microsoft.AspNetCore.Mvc.Core/Internal/DefaultAssemblyPartDiscoveryProvider.cs
    /// </remarks>
    internal static class Scanner
    {

        internal static void RegisterAttributedDependencies(IServiceCollection services, string applicationName)
        {
            foreach (var assembly in DiscoverAssemblyParts(applicationName))
            {
                foreach (var type in assembly.GetTypes())
                {
                    var typeInfo = type.GetTypeInfo();
                    var dependencyAttributes = typeInfo.GetCustomAttributes<DependencyAttribute>();
                    foreach (var dependencyAttribute in dependencyAttributes)
                    {
                        var serviceDescriptor = dependencyAttribute.BuildServiceDescriptor(typeInfo);
                        services.Add(serviceDescriptor);
                    }
                }
            }
        }

        private static HashSet<string> ReferenceAssemblies { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Slamby.Common",
            "Slamby.API",
            "Slamby.Cerebellum",
            "Slamby.Elastic"
        };

        private static IEnumerable<Assembly> DiscoverAssemblyParts(string entryPointAssemblyName)
        {
            var entryAssembly = Assembly.Load(new AssemblyName(entryPointAssemblyName));
            var context = DependencyContext.Load(Assembly.Load(new AssemblyName(entryPointAssemblyName)));

            return GetCandidateAssemblies(entryAssembly, context);
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies(Assembly entryAssembly, DependencyContext dependencyContext)
        {
            if (dependencyContext == null)
            {
                // Use the entry assembly as the sole candidate.
                return new[] { entryAssembly };
            }

            return GetCandidateLibraries(dependencyContext)
                .SelectMany(library => library.GetDefaultAssemblyNames(dependencyContext))
                .Select(Assembly.Load);
        }

        // Returns a list of libraries that references the assemblies in <see cref="ReferenceAssemblies"/>.
        // By default it returns all assemblies that reference any of the primary Slamby assemblies
        private static IEnumerable<RuntimeLibrary> GetCandidateLibraries(DependencyContext dependencyContext)
        {
            if (ReferenceAssemblies == null)
            {
                return Enumerable.Empty<RuntimeLibrary>();
            }

            var candidatesResolver = new CandidateResolver(dependencyContext.RuntimeLibraries, ReferenceAssemblies);
            return candidatesResolver.GetCandidates();
        }

        private sealed class CandidateResolver
        {
            private readonly IDictionary<string, Dependency> _dependencies;

            public CandidateResolver(IReadOnlyList<RuntimeLibrary> dependencies, ISet<string> referenceAssemblies)
            {
                _dependencies = dependencies
                    .ToDictionary(d => d.Name, d => CreateDependency(d, referenceAssemblies), StringComparer.OrdinalIgnoreCase);
            }

            private Dependency CreateDependency(RuntimeLibrary library, ISet<string> referenceAssemblies)
            {
                var classification = DependencyClassification.Unknown;
                if (referenceAssemblies.Contains(library.Name))
                {
                    //all Slamby assembly can be a candidate
                    classification = DependencyClassification.Candidate;
                }

                return new Dependency(library, classification);
            }

            private DependencyClassification ComputeClassification(string dependency)
            {
                Debug.Assert(_dependencies.ContainsKey(dependency));

                var candidateEntry = _dependencies[dependency];
                if (candidateEntry.Classification != DependencyClassification.Unknown) //if the entry is already classified
                {
                    return candidateEntry.Classification;
                }
                else
                {
                    var classification = DependencyClassification.NotCandidate;
                    foreach (var candidateDependency in candidateEntry.Library.Dependencies)
                    {
                        var dependencyClassification = ComputeClassification(candidateDependency.Name);
                        if (dependencyClassification == DependencyClassification.Candidate)//if it references a candidate library than it can be also a candidate
                        {
                            classification = DependencyClassification.Candidate;
                            break;
                        }
                    }

                    candidateEntry.Classification = classification;

                    return classification;
                }
            }

            public IEnumerable<RuntimeLibrary> GetCandidates()
            {
                foreach (var dependency in _dependencies)
                {
                    if (ComputeClassification(dependency.Key) == DependencyClassification.Candidate)
                    {
                        yield return dependency.Value.Library;
                    }
                }
            }

            private class Dependency
            {
                public Dependency(RuntimeLibrary library, DependencyClassification classification)
                {
                    Library = library;
                    Classification = classification;
                }

                public RuntimeLibrary Library { get; }

                public DependencyClassification Classification { get; set; }

                public override string ToString()
                {
                    return $"Library: {Library.Name}, Classification: {Classification}";
                }
            }

            private enum DependencyClassification
            {
                Unknown = 0,
                Candidate = 1,
                NotCandidate = 2
            }
        }
    }
}
