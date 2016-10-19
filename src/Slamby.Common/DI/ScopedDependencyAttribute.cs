using Microsoft.Extensions.DependencyInjection;

namespace Slamby.Common.DI
{
    /// <summary>
    /// Scoped lifetime services are created once per request.
    /// </summary>
    public class ScopedDependencyAttribute : DependencyAttribute
    {
        public ScopedDependencyAttribute()
            : base(ServiceLifetime.Scoped)
        { }
    }
}
