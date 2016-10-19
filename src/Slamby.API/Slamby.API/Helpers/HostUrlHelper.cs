using System;
using System.Linq;
using Microsoft.AspNet.Http;

namespace Slamby.API.Helpers
{
    public static class HostUrlHelper
    {
        public static string GetHostUrl(HttpRequest request, string baseUrlPrefix, string path = "")
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var hostUrl = string.Empty;
            
            if (string.IsNullOrWhiteSpace(baseUrlPrefix))
            {
                hostUrl = string.Format("{0}://{1}", request.Scheme, request.Host.ToUriComponent());
            }
            else
            {
                var hostName = request.Host
                .ToUriComponent()
                .Split(new[] { "." }, StringSplitOptions.None)
                .First();
                hostName = hostName.Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[0];
                hostUrl = string.Format("{0}/{1}", baseUrlPrefix, hostName);
            }

            return hostUrl + path;
        }
    }
}
