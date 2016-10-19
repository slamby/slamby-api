using Microsoft.Extensions.DependencyInjection;

namespace Slamby.Common.DI
{
    /// <summary>
    /// Singleton lifetime services are created the first time they are requested and then every subsequent request will use the same instance. 
    /// </summary>
    public class SingletonDependencyAttribute : DependencyAttribute
    {
        public SingletonDependencyAttribute()
            : base(ServiceLifetime.Singleton)
        { }
    }
}
