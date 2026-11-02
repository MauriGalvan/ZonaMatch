namespace ZonaMatch.Application.DTOs.PointsOfInterest.Responses;

// Truncated is true when there were more points than the limit and only the closest points are returned.
public sealed record NearbyPointsOfInterestResponse(IReadOnlyList<PointOfInterestResponse> Points, bool Truncated);
