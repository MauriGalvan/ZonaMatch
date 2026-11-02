using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Interfaces
{
    // Repository for entities geolocated by a point (latitude/longitude).
    public interface IPointEntityRepository<T> : IRepository<T> where T : class, IPointLocation
    {
        // Entities within the given radius, ordered by ascending distance.
        Task<IReadOnlyList<(T Entity, double DistanceMeters)>> GetNearbyAsync(double latitude, double longitude, double radiusMeters);

        // The nearest entities without a radius limit, ordered by ascending distance.
        Task<IReadOnlyList<(T Entity, double DistanceMeters)>> GetNearestAsync(double latitude, double longitude, int count);
    }
}
