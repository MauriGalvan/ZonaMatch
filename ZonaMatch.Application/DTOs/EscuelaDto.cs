namespace ZonaMatch.Application.DTOs
{
    public record EscuelaDto(
        int Id,
        string Nombre,
        string? Nivel,
        string? Gestion,
        string? Direccion,
        double Latitud,
        double Longitud,
        double? DistanciaMetros = null);
}
