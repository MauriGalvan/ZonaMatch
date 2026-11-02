using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // Returns GeoJSON FeatureCollections (as JSON text) built by the database, ready to send to the map.
    // "tolerancia" is the simplification tolerance in degrees (0 = no simplification).
    public interface IMapRepository
    {
        Task<string> GetDistrictsAsync(double tolerancia);
        Task<string> GetCommunesAsync(double tolerancia);

        Task<int> CountCensusTractsAsync(BoundingBox bbox);
        Task<string> GetCensusTractsAsync(BoundingBox bbox, double tolerancia);

        Task<int> CountSchoolsAsync(BoundingBox bbox, string? nivel, string? sector);
        Task<string> GetSchoolsAsync(BoundingBox bbox, string? nivel, string? sector);
    }
}
