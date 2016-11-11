using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Slamby.Common.Config;
using Microsoft.AspNetCore.Server.Kestrel.Internal.Http;

namespace Slamby.API.Middlewares
{
    public class ElmSecurityMiddleware
    {
        readonly RequestDelegate next;
        readonly SiteConfig siteConfig;
        const string SessionKey = "elm-authenticated-user";

        public ElmSecurityMiddleware(RequestDelegate next, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var isElmUrl = context.Request.Path.StartsWithSegments("/elm");
            var isSessionAuthenticated = !string.IsNullOrWhiteSpace(context.Session.GetString(SessionKey));

            if (!isElmUrl || isSessionAuthenticated)
            {
                await next.Invoke(context);
                return;
            }

            var basicAuth = new BasicAuthenticationParser(context);

            if (string.Equals(basicAuth.Username, "slamby", StringComparison.Ordinal) &&
                string.Equals(basicAuth.Password, siteConfig.Elm.Key, StringComparison.Ordinal))
            {
                context.Session.SetString(SessionKey, basicAuth.Username);
                await next.Invoke(context);
                return;
            }

            context.Response.StatusCode = 401;
            context.Response.Headers.Add("WWW-Authenticate", new[] { "Basic" });
        }
    }

    internal class BasicAuthenticationParser
    {
        public string Username { get; }

        public string Password { get; }

        public BasicAuthenticationParser(HttpContext context)
        {
            var parts = GetCredentials(context).Split(':');

            if (parts.Length == 2)
            {
                Username = parts[0];
                Password = parts[1];
            }
        }

        private string GetCredentials(HttpContext context)
        {
            try
            {
                var authorizationHeader = ((FrameRequestHeaders)context.Request.Headers).HeaderAuthorization;

                if (authorizationHeader.Any(headerValue => headerValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)))
                {
                    var value = Convert.FromBase64String(authorizationHeader[0].Split(' ')[1]);
                    return Encoding.UTF8.GetString(value);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}