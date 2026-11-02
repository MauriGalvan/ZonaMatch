namespace ZonaMatch.Application.DTOs.PointsOfInterest.Responses;

// Truncado is true when there were more points than the limit and only the closest points are returned.
public sealed record NearbyPointsOfInterestResponse(IReadOnlyList<PointOfInterestResponse> Puntos, bool Truncado);
