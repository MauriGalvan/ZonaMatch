namespace ZonaMatch.Api.Authentication;

internal static class AuthCookieDefaults
{
    public const string Name = "__Host-zonamatch-auth";

    public static CookieOptions CreateOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/",
        Expires = expires,
    };
}
