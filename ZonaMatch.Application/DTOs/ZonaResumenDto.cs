namespace ZonaMatch.Application.DTOs
{
    // A zone found by name: enough to list it and open GET /Zonas/{Slug}.
    // Jurisdicciones: the boundaries that contain it, most specific first (e.g. Comuna 10, CABA).
    public record ZonaResumenDto(
        string Slug,
        string Nombre,
        int NivelAdministrativo,
        IReadOnlyList<JurisdiccionDto> Jurisdicciones,
        double SuperficieKm2);

    public record JurisdiccionDto(string Nombre, int NivelAdministrativo);
}
