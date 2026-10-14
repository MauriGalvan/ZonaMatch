namespace ZonaMatch.Application.DTOs
{
    // GET /Zonas/{slug}/transporte: colectivos, trenes y subte de la zona en un dia habil, por franja horaria
    public record TransporteZonaDto(
        string Slug,
        IReadOnlyList<ModoTransporteDto> Modos,
        IReadOnlyList<FuenteDatoDto> Fuentes);

    // Modo: "colectivo", "tren" o "subte"
    public record ModoTransporteDto(
        string Modo,
        int Paradas,
        int CantidadLineas,
        IReadOnlyList<FranjaTransporteDto> Franjas,
        // Las de mas servicio en la hora pico de la manana, primero
        IReadOnlyList<LineaTransporteDto> Lineas);

    // Salidas por hora de todas las lineas juntas en la franja
    public record FranjaTransporteDto(
        string Franja,
        int Desde,
        int Hasta,
        int LineasConServicio,
        double SalidasPorHora);

    // Una linea: salidas por hora y cada cuantos minutos pasa en cada franja
    public record LineaTransporteDto(
        string Linea,
        int Paradas,
        IReadOnlyList<FrecuenciaLineaDto> Franjas);

    public record FrecuenciaLineaDto(string Franja, double SalidasPorHora, int? MinutosEntreSalidas);
}
