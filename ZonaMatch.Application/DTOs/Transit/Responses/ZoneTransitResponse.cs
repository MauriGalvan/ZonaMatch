using ZonaMatch.Application.DTOs.Shared.Responses;

namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Public transit in a zone on a weekday, grouped by time of day.
public sealed record ZoneTransitResponse(
    string Slug,
    IReadOnlyList<TransitModeResponse> Modos,
    IReadOnlyList<DataSourceResponse> Fuentes);
