using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Slamby.SDK.Net.Models;
using System.IO;
using Slamby.Common.Config;
using Slamby.API.Helpers;

namespace Slamby.API.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger logger;
        readonly SiteConfig siteConfig;

        public GlobalExceptionFilter(ILoggerFactory loggerFactory, SiteConfig siteConfig)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            logger = loggerFactory.CreateLogger("Global Exception Filter");
            this.siteConfig = siteConfig;
        }

        public void OnException(ExceptionContext context)
        {
            var message = "Internal Server Error";

            if (context.Exception is Common.Exceptions.SlambyException && !(context.Exception is Common.Exceptions.ElasticSearchException)) message = context.Exception.Message;

            var response = ErrorsModel.Create(message);

            context.Result = new ObjectResult(response)
            {
                StatusCode = 500,
                DeclaredType = typeof(ErrorsModel)
            };

            logger.LogError(new EventId(0), context.Exception, "GlobalExceptionFilter");

            //log the request for this error if it had
            // Replace Request Body with own MemoryStream
            var originalRequestBody = context.HttpContext.Request.Body;
            var requestBodyStream = new MemoryStream();
            context.HttpContext.Request.Body.CopyTo(requestBodyStream);

            requestBodyStream.Seek(0, SeekOrigin.Begin);

            logger.LogInformation(RequestLoggerHelper.Format(context.HttpContext.Request, "GlobalExceptionFilter", requestBodyStream, siteConfig.BaseUrlPrefix));
            requestBodyStream.Seek(0, SeekOrigin.Begin);

            context.HttpContext.Request.Body = requestBodyStream;

            context.Exception = null; // mark exception as handled
        }
    }
}
