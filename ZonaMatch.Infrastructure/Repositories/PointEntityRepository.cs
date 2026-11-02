using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories;

// Base repository for entities located by a point (column "geom": geometry(Point,4326), units in degrees).
public class PointEntityRepository<T> : Repository<T>, IPointEntityRepository<T> where T : class, IPointLocation
{
    public PointEntityRepository(ZonaMatchDbContext dbContext) : base(dbContext)
    {
    }

    public Task<IReadOnlyList<(T Entity, double DistanceMeters)>> GetNearbyAsync(
        double latitude, double longitude, double radiusMeters) =>
        FindWithinRadiusAsync(_dbContext.Set<T>(), latitude, longitude, radiusMeters);

    public async Task<IReadOnlyList<(T Entity, double DistanceMeters)>> GetNearestAsync(
        double latitude, double longitude, int count)
    {
        // Equirectangular distance computed in SQL orders correctly at city scale, unlike raw degrees.
        var cosineLatitude = Math.Cos(latitude * Math.PI / 180d);

        var candidates = await _dbContext.Set<T>()
            .OrderBy(entity => Math.Pow(entity.Ubicacion.Y - latitude, 2) + Math.Pow((entity.Ubicacion.X - longitude) * cosineLatitude, 2))
            .Take(count)
            .ToListAsync();

        return WithDistance(candidates, latitude, longitude);
    }

    // Shared by derived repositories that pre-filter a query (for example, by level) before searching by radius.
    protected static async Task<IReadOnlyList<(T Entity, double DistanceMeters)>> FindWithinRadiusAsync(
        IQueryable<T> query, double latitude, double longitude, double radiusMeters)
    {
        var point = GeoFactory.CreatePoint(latitude, longitude);
        var radiusDegrees = GeoMath.ConvertMetersToDegrees(radiusMeters, latitude);

        // ST_DWithin in degrees is a coarse, index-friendly prefilter; exact meters are computed in memory.
        var candidates = await query
            .Where(entity => entity.Ubicacion.IsWithinDistance(point, radiusDegrees))
            .ToListAsync();

        return WithDistance(candidates, latitude, longitude)
            .Where(item => item.DistanceMeters <= radiusMeters)
            .ToList();
    }

    private static IReadOnlyList<(T Entity, double DistanceMeters)> WithDistance(
        IEnumerable<T> entities, double latitude, double longitude) =>
        entities
            .Select(entity => (Entity: entity,
                DistanceMeters: GeoMath.CalculateDistanceInMeters(latitude, longitude, entity.Ubicacion.Y, entity.Ubicacion.X)))
            .OrderBy(item => item.DistanceMeters)
            .ToList();
}
