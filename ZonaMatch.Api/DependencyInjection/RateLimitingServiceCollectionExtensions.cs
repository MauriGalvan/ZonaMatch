using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using ZonaMatch.Api.Authentication;

namespace ZonaMatch.Api.DependencyInjection;

internal static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddRequestRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RequestRateLimitOptions>()
            .BindConfiguration(RequestRateLimitOptions.SectionName)
            .Validate(options => options.Login.IsValid, "RateLimits:Login requires a positive permit limit and window.")
            .Validate(options => options.Contact.IsValid, "RateLimits:Contact requires a positive permit limit and window.")
            .Validate(options => options.PasswordReset.IsValid, "RateLimits:PasswordReset requires a positive permit limit and window.")
            .Validate(options => options.InvitationEmail.IsValid, "RateLimits:InvitationEmail requires a positive permit limit and window.")
            .Validate(options => options.GuestReport.IsValid, "RateLimits:GuestReport requires a positive permit limit and window.")
            .Validate(options => options.ReportDownload.IsValid, "RateLimits:ReportDownload requires a positive permit limit and window.")
            .Validate(options => options.ImageUpload.IsValid, "RateLimits:ImageUpload requires a positive permit limit and window.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var http = context.HttpContext;
                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ZonaMatch.RateLimiting")
                    .LogWarning("Rate limit exceeded on {Method} {Path} from {RemoteIp}",
                        http.Request.Method, http.Request.Path, http.Connection.RemoteIpAddress);
                return ValueTask.CompletedTask;
            };
        });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RequestRateLimitOptions>>((options, configuration) =>
            {
                var limits = configuration.Value;
                options.AddPolicy("login", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.Login.ToLimiterOptions()));
                options.AddPolicy("contact", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.Contact.ToLimiterOptions()));
                options.AddPolicy("password-reset", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.PasswordReset.ToLimiterOptions()));
                options.AddPolicy("invitation-email", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.GetAuthenticatedUserId()?.ToString() ?? GetClientIp(context),
                        _ => limits.InvitationEmail.ToLimiterOptions()));
                options.AddPolicy("guest-report", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.GuestReport.ToLimiterOptions()));
                options.AddPolicy("report-download", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.ReportDownload.ToLimiterOptions()));
                options.AddPolicy("image-upload", context =>
                    RateLimitPartition.GetFixedWindowLimiter(GetClientIp(context), _ => limits.ImageUpload.ToLimiterOptions()));
            });

        return services;
    }

    private static string GetClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
