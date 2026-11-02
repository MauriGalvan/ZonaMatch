using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // Returns GeoJSON FeatureCollections (as JSON text) built by the database, ready to send to the map.
    // Simplification tolerance in degrees (0 = no simplification).
    public interface IMapRepository
    {
        Task<string> GetDistrictsAsync(double simplificationTolerance);
        Task<string> GetCommunesAsync(double simplificationTolerance);

        Task<int> CountCensusTractsAsync(BoundingBox boundingBox);
        Task<string> GetCensusTractsAsync(BoundingBox boundingBox, double simplificationTolerance);

        Task<int> CountSchoolsAsync(BoundingBox boundingBox, string? level, string? sector);
        Task<string> GetSchoolsAsync(BoundingBox boundingBox, string? level, string? sector);
    }
}
