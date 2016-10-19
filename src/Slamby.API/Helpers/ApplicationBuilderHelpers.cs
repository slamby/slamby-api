using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Helpers
{
    public static class ApplicationBuilderHelpers
    {
        public static void WriteExceptionResponse(this IApplicationBuilder app, ILogger log, Exception ex, string message)
        {
            app.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                log.LogError(ex.Message);
                log.LogError(ex.StackTrace);

                await context.Response.WriteAsync(ErrorsModelResponse(message)).ConfigureAwait(false);
            });
        }

        public static void WriteExceptionsResponse(this IApplicationBuilder app, ILogger log, Dictionary<string, List<Exception>> exceptions, string message)
        {
            app.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                foreach (var ex in exceptions)
                {
                    foreach (var innerException in ex.Value)
                    {
                        log.LogError($"{ex.Key}:::{innerException.Message}");
                        log.LogError(innerException.StackTrace);
                    }
                }

                await context.Response.WriteAsync(ErrorsModelResponse(message)).ConfigureAwait(false);
            });
        }

        public static string ErrorsModelResponse(string errorMessage)
        {
            return JsonConvert.SerializeObject(
                ErrorsModel.Create(errorMessage),
                Formatting.Indented);
        }
    }
}