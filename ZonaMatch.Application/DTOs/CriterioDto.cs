namespace ZonaMatch.Application.DTOs
{
    // GET /Criterios: catalogo de criterios activos del wizard, en orden de aparicion.
    // Icono es la clase de PrimeIcons ("pi pi-shield"); el front lo renombra a icon.
    public record CriterioDto(string Id, string Nombre, string Descripcion, string Icono, int Orden);
}
