using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Geo;

namespace ZonaMatch.Infrastructure.Repositories;

// Base repository for entities geolocated by an area (a geometry column).
public class PolygonRepository<T> : Repository<T>, IPolygonRepository<T> where T : class, IPolygonLocation
{
    // One degree of latitude is approximately 111,320 meters. Geometry columns in SRID 4326 use degrees.
    // Reproject to a metric SRID or migrate the column to geography when exact distance calculations are required.
    private const double MetersPerDegree = 111_320d;

    public PolygonRepository(ZonaMatchDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<T?> GetContainingPointAsync(double latitude, double longitude)
    {
        var point = GeoFactory.CreatePoint(latitude, longitude);

        try
        {
            return await _dbContext.Set<T>()
                .FirstOrDefaultAsync(entity => entity.Geometry.Contains(point));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // If the geo dump (zonamatch.sql) is missing, return no result instead of breaking dependent queries.
            return null;
        }
    }

    public async Task<IReadOnlyList<T>> GetNearbyAsync(double latitude, double longitude, double radiusMeters)
    {
        var point = GeoFactory.CreatePoint(latitude, longitude);
        var radiusDegrees = radiusMeters / MetersPerDegree;

        return await _dbContext.Set<T>()
            .Where(entity => entity.Geometry.IsWithinDistance(point, radiusDegrees))
            .ToListAsync();
    }
}
