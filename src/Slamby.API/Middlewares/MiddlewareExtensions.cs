using LimitsMiddleware.Extensions;
using Microsoft.AspNetCore.Builder;

namespace Slamby.API.Middlewares
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseApiHeaderAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiHeaderAuthenticationMiddleware>();
        }
        
        public static IApplicationBuilder UseApiHeaderVersion(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiHeaderVersionMiddleware>();
        }

        public static IApplicationBuilder UseElapsedTime(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ElapsedTimeMiddleware>();
        }

        public static IApplicationBuilder UseGzip(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GzipMiddleware>();
        }

        public static IApplicationBuilder UseRequestLogger(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggerMiddleware>();
        }

        public static IApplicationBuilder UseRequestSizeLimit(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestSizeLimitMiddleware>();
        }

        public static IApplicationBuilder UseElmSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ElmSecurityMiddleware>();
        }

        public static IApplicationBuilder UseElmStyleUrlFix(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ElmStyleUrlFixMiddleware>();
        }

        public static IApplicationBuilder UseNotFound(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<NotFoundMiddleware>();
        }

        public static IApplicationBuilder UseSecretValidator(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecretValidatorMiddleware>();
        }

        public static IApplicationBuilder UsePathBase(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PathBaseMiddleware>();
        }

        public static IApplicationBuilder UseTerminal(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TerminalMiddleware>();
        }

        public static IApplicationBuilder UseConcurrentRequestsLimit(this IApplicationBuilder builder, int limit)
        {
            return builder.MaxConcurrentRequests(limit);
        }
    }
}
