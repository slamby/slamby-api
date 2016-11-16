using System.Collections.Generic;
using System.Threading.Tasks;
using Slamby.License.Core.Validation;

namespace Slamby.API.Services
{
    public interface ILicenseManager
    {
        string ApplicationId { get; }
        string InstanceId { get; }

        void EnsureAppIdCreated();

        string Get();
        Task<IEnumerable<ValidationFailure>> SaveAsync(string text);

        Task<IEnumerable<ValidationFailure>> ValidateAsync(string licenseKey);
    }
}