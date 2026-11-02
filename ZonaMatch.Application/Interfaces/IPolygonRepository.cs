using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repository for entities represented by an area (polygon or multipolygon).
    public interface IPolygonRepository<T> : IRepository<T> where T : class, IPolygonLocation
    {
        // The entity whose area contains the given point (for example, the district containing an address).
        Task<T?> GetContainingPointAsync(double latitude, double longitude);

        // Entities whose area is approximately within the given radius in meters from the point.
        Task<IReadOnlyList<T>> GetNearbyAsync(double latitude, double longitude, double radiusMeters);
    }
}
