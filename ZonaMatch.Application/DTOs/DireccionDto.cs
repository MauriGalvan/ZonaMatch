namespace ZonaMatch.Application.DTOs
{
    // Place name and address of a point. Everything except the coordinates is optional:
    // an arbitrary map point may have no name (empty lot) or no street number.
    // Partido and Comuna come from our own GIS data (geo schema); the rest comes from the geocoder.
    public record DireccionDto(
        CoordenadaDto Ubicacion,
        string? Nombre,
        string? Calle,
        string? Altura,
        string? Localidad,
        string? Partido,
        string? Comuna,
        string? Provincia,
        string? CodigoPostal,
        string? DireccionCompleta);
}
