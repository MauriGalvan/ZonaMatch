namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Paths of one line inside the zone; each point is [latitude, longitude].
public sealed record TransitRouteResponse(string Mode, string Line, IReadOnlyList<IReadOnlyList<double[]>> Paths);
