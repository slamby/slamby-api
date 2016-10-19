using Microsoft.Extensions.DependencyInjection;

namespace Slamby.Common.DI
{
    /// <summary>
    /// Transient lifetime services are created each time they are requested. This lifetime works best for lightweight, stateless services.
    /// </summary>
    public class TransientDependencyAttribute : DependencyAttribute
    {
        public TransientDependencyAttribute()
            : base(ServiceLifetime.Transient)
        { }
    }
}
