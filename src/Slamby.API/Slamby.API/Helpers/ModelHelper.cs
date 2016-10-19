using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;

namespace Slamby.API.Helpers
{
    public static class ModelHelper
    {
        public static T ToServiceModel<T>(this ServiceElastic service) where T : Service, new()
        {
            return new T
            {
                Id = service.Id,
                Name = service.Name,
                Alias = service.Alias,
                Description = service.Description,
                Type = (ServiceTypeEnum)service.Type,
                Status = (ServiceStatusEnum)service.Status,
                ProcessIdList = service.ProcessIdList,
                ActualProcessId = null
            };
        }

        public static Process ToProcessModel(this ProcessElastic process)
        {
            var model = new Process
            {
                Id = process.Id,
                Start = process.Start,
                End = process.End,
                Percent = process.Percent,
                Description = process.Description,
                Status = (ProcessStatusEnum)process.Status,
                ErrorMessages = process.ErrorMessages,
                ResultMessage = process.ResultMessage,
                Type = (ProcessTypeEnum)process.Type
            };

            return model;
        }
    }
}
