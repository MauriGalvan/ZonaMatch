using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // Returns GeoJSON FeatureCollections (as JSON text) built by the database, ready to send to the map.
    // "tolerancia" is the simplification tolerance in degrees (0 = no simplification).
    public interface IMapaRepository
    {
        Task<string> GetPartidosAsync(double tolerancia);
        Task<string> GetComunasAsync(double tolerancia);

        Task<int> ContarRadiosAsync(BoundingBox bbox);
        Task<string> GetRadiosAsync(BoundingBox bbox, double tolerancia);

        Task<int> ContarEscuelasAsync(BoundingBox bbox, string? nivel, string? sector);
        Task<string> GetEscuelasAsync(BoundingBox bbox, string? nivel, string? sector);
    }
}
