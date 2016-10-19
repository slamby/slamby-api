using Slamby.Elastic.Models;

namespace Slamby.API.Helpers.Services.Interfaces
{
    public interface IServiceHandler<TServiceSettings> 
        where TServiceSettings : BaseServiceSettingsElastic
    {
        void Activate(string processId, TServiceSettings settings, System.Threading.CancellationToken token);
    }
}
