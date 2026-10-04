namespace ZonaMatch.Application.DTOs
{
    // Raw result of a reverse-geocoding provider, before being completed with our own GIS data
    public record DireccionGeocodificadaDto(
        string? Nombre,
        string? Calle,
        string? Altura,
        string? Localidad,
        string? Provincia,
        string? CodigoPostal,
        string? DireccionCompleta);
}
