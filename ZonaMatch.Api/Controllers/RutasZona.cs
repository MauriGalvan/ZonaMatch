namespace ZonaMatch.Api.Controllers
{
    // Route constraint of the zone slug (same format as SlugZona.EsValido): a malformed slug gives 404.
    // Brackets are doubled because they are special in route templates.
    internal static class RutasZona
    {
        public const string Slug = "^[[a-z0-9]]+(-[[a-z0-9]]+)*$";
    }
}
