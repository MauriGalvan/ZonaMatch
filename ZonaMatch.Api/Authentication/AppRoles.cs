namespace ZonaMatch.Api.Authentication;

// Roles carried in the access token ("role" claim)
public static class AppRoles
{
    public const string ClaimType = "role";

    // Site administrator (User.IsAdministrator)
    public const string Admin = "admin";
}
