namespace ZonaMatch.Application.DTOs
{
    // GET /api/v1/criteria: active wizard criteria, ordered by display position.
    // Icono es la clase de PrimeIcons ("pi pi-shield"); el front lo renombra a icon.
    public record CriterioDto(string Id, string Nombre, string Descripcion, string Icono, int Orden);
}
