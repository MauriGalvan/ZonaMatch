using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.Models.PointsOfInterest;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Services;

/// <summary>
/// Shared response shaping for point-of-interest queries by location and by zone.
/// </summary>
public sealed class PointOfInterestResponseBuilder
{
    // Covers large neighborhoods while keeping GeoJSON and map payloads manageable.
    public const int MaximumPoints = 5_000;

    // The extra row indicates whether the repository result was truncated.
    public NearbyPointsOfInterestResponse BuildPoints(IReadOnlyList<PointOfInterestResponse> points) =>
        points.Count > MaximumPoints
            ? new NearbyPointsOfInterestResponse(points.Take(MaximumPoints).ToList(), Truncated: true)
            : new NearbyPointsOfInterestResponse(points, Truncated: false);

    public PointsOfInterestSummaryResponse BuildSummary(
        double? radiusMeters,
        IReadOnlyList<PointOfInterestCount> counts) =>
        new(radiusMeters, BuildCategories(counts));

    private static IReadOnlyList<PointOfInterestCategorySummaryResponse> BuildCategories(IReadOnlyList<PointOfInterestCount> counts) =>
        PointOfInterestCategory.All
            .Select(category =>
            {
                var types = counts
                    .Where(count => count.Category == category)
                    .OrderByDescending(count => count.Count)
                    .Select(count => new PointOfInterestTypeCountResponse(count.Type, count.Count))
                    .ToList();

                return new PointOfInterestCategorySummaryResponse(category, types.Sum(type => type.Count), types);
            })
            .ToList();
}
