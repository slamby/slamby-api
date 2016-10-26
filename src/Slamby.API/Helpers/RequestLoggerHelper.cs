using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Helpers
{
    public static class RequestLoggerHelper
    {
        public static string FormatHeaders(IHeaderDictionary headers)
        {
            return string.Join("\n", headers.Select(h => string.Format(h.Key + "|" + string.Join(" ", h.Value))));
        }

        public static string Format(HttpRequest request, string requestId, Stream bodyStream, string baseUrlPrefix)
        {
            var text = string.Format(
                "REQUEST #{3}\n" +
                "----------------------\n" +
                "{0} {1}\n" +
                "Headers:\n{2}\n\n",
                request.Method,
                HostUrlHelper.GetHostUrl(request, baseUrlPrefix, request.Path),
                FormatHeaders(request.Headers),
                requestId);

            if (bodyStream != null)
            {
                var body = new StreamReader(bodyStream).ReadToEnd();
                if (!string.IsNullOrEmpty(body))
                {
                    text += $"Content:\n{body}\n\n";
                }
            }
            else
            {
                text += $"Content:\n!!!REMOVED FROM LOG!!!\n\n";
            }

            return text;
        }

        public static string Format(HttpResponse response, string requestId, Stream bodyStream)
        {
            var text = string.Format(
                "RESPONSE #{4}\n" +
                "----------------------\n" +
                "{0} {1} {2}\n" +
                "Headers:\n{3}\n\n",
                response.StatusCode,
                ReasonPhrases.GetReasonPhrase(response.StatusCode),
                response.HttpContext.Features.Get<IHttpResponseFeature>()?.ReasonPhrase, // Custom reason phrase if it is defined
                FormatHeaders(response.Headers),
                requestId);

            if (bodyStream != null)
            {
                var body = new StreamReader(bodyStream).ReadToEnd();
                if (!string.IsNullOrEmpty(body))
                {
                    text += $"Content:\n{body}\n\n";
                }
            }
            else
            {
                text += $"Content:\n!!!REMOVED FROM LOG!!!\n\n";
            }

            return text;
        }
    }
}
