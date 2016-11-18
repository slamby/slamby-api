using System;
using System.Threading.Tasks;
using Slamby.License.Core.Models;

namespace Slamby.API.Services.Interfaces
{
    public interface ILicenseServerClient
    {
        Task<CreateResponseModel> Create(string email, Guid instanceId);
        Task<CheckResponseModel> RequestCheck(string xmlText, Guid instanceId, DateTime launchTime);
    }
}