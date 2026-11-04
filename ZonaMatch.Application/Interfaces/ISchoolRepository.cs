using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ISchoolRepository : IPointEntityRepository<School>
    {
        // Same as GetNearbyAsync, with optional filters by school level and sector.
        Task<IReadOnlyList<(School Entity, double DistanceMeters)>> GetNearbyByFiltersAsync(
            double latitude, double longitude, double radiusMeters, string? level, string? sector);
    }
}
