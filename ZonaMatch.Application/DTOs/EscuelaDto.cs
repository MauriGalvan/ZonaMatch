namespace ZonaMatch.Application.DTOs
{
    public record EscuelaDto(
        string ClaveNatural,
        string Nombre,
        string? Nivel,
        string? Sector,
        string? Direccion,
        string? Localidad,
        double Latitud,
        double Longitud,
        double? DistanciaMetros = null);
}
