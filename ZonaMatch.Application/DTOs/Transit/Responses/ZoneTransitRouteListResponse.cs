namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Transit lines that run through a zone, for the map layer.
public sealed record ZoneTransitRouteListResponse(string Slug, IReadOnlyList<TransitRouteResponse> Routes);
