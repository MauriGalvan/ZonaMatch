namespace ZonaMatch.Application.Models.PointsOfInterest;

// Internal projection of the point-of-interest summary query.
public sealed record PointOfInterestCount(string Category, string? Type, int Count);
