using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    // GeoJSON for the map. Methods that take a viewport return null when it holds too many features
    // (the caller should ask the user to zoom in or filter).
    public interface IMapaService
    {
        Task<string> GetPartidosAsync(double? tolerancia);
        Task<string> GetComunasAsync(double? tolerancia);
        Task<string?> GetRadiosAsync(BoundingBox bbox, double? tolerancia);
        Task<string?> GetEscuelasAsync(BoundingBox bbox, string? nivel, string? sector);
    }
}
