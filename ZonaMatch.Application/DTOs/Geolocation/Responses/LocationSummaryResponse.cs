namespace ZonaMatch.Application.DTOs.Geolocation.Responses;

// A point belongs to a commune (CABA) or district (the rest of Buenos Aires), so either one may be null.
public sealed record LocationSummaryResponse(
    CoordinateResponse Location,
    DistrictResponse? District,
    CommuneResponse? Commune,
    IReadOnlyList<SchoolResponse> NearbySchools);
