using System.Threading.RateLimiting;

namespace ZonaMatch.Api.DependencyInjection;

internal sealed class RequestRateLimitOptions
{
    public const string SectionName = "RateLimits";

    public FixedWindow Login { get; set; } = new();
    public FixedWindow Contact { get; set; } = new();
    public FixedWindow PasswordReset { get; set; } = new();
    public FixedWindow InvitationEmail { get; set; } = new();
    public FixedWindow GuestReport { get; set; } = new();
    public FixedWindow ImageUpload { get; set; } = new();

    public sealed class FixedWindow
    {
        public int PermitLimit { get; set; }
        public TimeSpan Window { get; set; }

        public bool IsValid => PermitLimit > 0 && Window > TimeSpan.Zero;

        public FixedWindowRateLimiterOptions ToLimiterOptions() => new()
        {
            PermitLimit = PermitLimit,
            Window = Window
        };
    }
}
