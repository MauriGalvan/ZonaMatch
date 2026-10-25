namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisZoneResponse(
    string Slug,
    string Name,
    bool IsInitial,
    bool IsAdded,
    // Starting point marked inside the zone; null = the center of the zone
    AnalysisZoneOriginResponse? Origin = null);

public sealed record AnalysisZoneOriginResponse(double Latitude, double Longitude);
