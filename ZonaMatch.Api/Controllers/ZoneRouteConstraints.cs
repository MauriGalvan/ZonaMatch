namespace ZonaMatch.Api.Controllers
{
    // Route constraint for a zone slug (same format as ZoneSlug.IsValid); malformed slugs return 404.
    // Braces are doubled because they have a special meaning in route templates.
    internal static class ZoneRouteConstraints
    {
        public const string Slug = "^[[a-z0-9]]+(-[[a-z0-9]]+)*$";
    }
}
