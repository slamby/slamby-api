using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Slamby.License.Core.Models;
using Slamby.License.Core.Validation;

namespace Slamby.API.Services
{
    public interface ILicenseManager
    {
        Guid InstanceId { get; }

        License.Core.License ApplicationLicense { get; }

        IEnumerable<ValidationFailure> ApplicationLicenseValidation { get; }

        void EnsureAppIdCreated();

        string Get();

        Task<IEnumerable<ValidationFailure>> SaveAsync(string text);

        Task<IEnumerable<ValidationFailure>> ValidateAsync(string licenseKey);

        Task<CreateResponseModel> Create(string email);

        void StartBackgroundValidator();
    }
}