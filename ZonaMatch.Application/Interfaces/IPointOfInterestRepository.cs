using ZonaMatch.Application.DTOs.PointsOfInterest.Responses;
using ZonaMatch.Application.DTOs.ZoneRecommendations;
using ZonaMatch.Application.Models.PointsOfInterest;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPointOfInterestRepository
    {
        // Points in the given categories and radius (meters), closest first, up to the specified limit.
        Task<IReadOnlyList<PointOfInterestResponse>> GetNearbyAsync(
            double latitude, double longitude, double radiusMeters, IReadOnlyCollection<string> categories, int limit,
            CancellationToken cancellationToken = default);

        // Number of points per (category, type) within the radius; only pairs with at least one point.
        Task<IReadOnlyList<PointOfInterestCount>> CountByTypeAsync(
            double latitude, double longitude, double radiusMeters, CancellationToken cancellationToken = default);

        // Same as above for the points inside a zone (same zone selection as GET /api/v1/zones/{slug}); distances are
        // measured from the center of the zone. An unknown slug gives no points.
        Task<IReadOnlyList<PointOfInterestResponse>> GetInZoneAsync(
            string slug, IReadOnlyCollection<string> categories, int limit, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PointOfInterestCount>> CountByTypeInZoneAsync(string slug, CancellationToken cancellationToken = default);

        // Profile-specific OSM places inside a zone, limited per recommendation category.
        Task<IReadOnlyList<ProfileRecommendationCandidate>> GetProfileRecommendationsInZoneAsync(
            string slug,
            IReadOnlyCollection<string> categories,
            int limitPerCategory,
            (double Latitude, double Longitude)? origin = null,
            CancellationToken cancellationToken = default);
    }
}
