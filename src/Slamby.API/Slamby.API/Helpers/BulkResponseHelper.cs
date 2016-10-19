using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Http;
using Slamby.SDK.Net.Models;
using Slamby.Elastic.Models;

namespace Slamby.API.Helpers
{
    public static class BulkResponseHelper
    {
        public static List<BulkResult> ToBulkResult(this IEnumerable<NestBulkResponse> responses)
        {
            var result = new List<BulkResult>();

            foreach (var response in responses)
            {
                result.AddRange(response.ToBulkResult());
            }

            return result;
        }

        public static List<BulkResult> ToBulkResult(this NestBulkResponse response)
        {
            var result = new List<BulkResult>();

            if (response.Items != null)
            {
                result.AddRange(response.Items.Where(i =>i.IsValid)
                    .Select(item => BulkResult.Create(item.Id, StatusCodes.Status200OK, string.Empty)));
            }

            if (response.ItemsWithErrors != null)
            {
                result.AddRange(response.ItemsWithErrors
                    .Select(item => BulkResult.Create(item.Id, StatusCodes.Status406NotAcceptable, item.Error.Reason)));
            }

            return result;
        }

        public static ErrorsModel ToErrorsModel(this NestBulkResponse response)
        {
            return ErrorsModel.Create(response.ToList());
        }

        public static List<string> ToList(this NestBulkResponse response)
        {
            return response.ItemsWithErrors.Select(i => i.Error.Reason).ToList();
        }
    }
}