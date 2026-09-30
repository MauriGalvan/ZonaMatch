namespace ZonaMatch.Application.DTOs
{
    public record EspacioVerdeDto(
        int Id,
        string Nombre,
        string? Tipo,
        double? SuperficieM2,
        double Latitud,
        double Longitud,
        double? DistanciaMetros = null);
}
