namespace ZonaMatch.Api.Controllers
{
    // Restriccion de ruta del slug de una zona (mismo formato que SlugZona.EsValido): un slug mal formado da 404.
    // Los corchetes van duplicados porque son especiales en las plantillas de rutas.
    internal static class RutasZona
    {
        public const string Slug = "^[[a-z0-9]]+(-[[a-z0-9]]+)*$";
    }
}
