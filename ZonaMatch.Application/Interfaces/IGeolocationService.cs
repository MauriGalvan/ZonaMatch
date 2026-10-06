using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IGeolocationService
    {
        // Partido/comuna containing the point plus the schools within the radius
        Task<UbicacionResumenDto> GetResumenUbicacionAsync(double latitud, double longitud, double radioMetros);

        Task<IReadOnlyList<EscuelaDto>> GetEscuelasCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel = null, string? sector = null);

        Task<IReadOnlyList<EscuelaDto>> GetEscuelasMasCercanasAsync(double latitud, double longitud, int cantidad);

        Task<PartidoDto?> GetPartidoPorUbicacionAsync(double latitud, double longitud);

        Task<ComunaDto?> GetComunaPorUbicacionAsync(double latitud, double longitud);

        // OpenStreetMap points of interest of the given categories within the radius, closest first
        Task<PuntosInteresCercanosDto> GetPuntosInteresCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias,
            CancellationToken cancellationToken = default);

        // Count of points of interest per category and type within the radius (every category is listed)
        Task<ResumenPuntosInteresDto> GetResumenPuntosInteresAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default);

        // Place name + address of a point (reverse geocoding), completed with the partido/comuna from our GIS data.
        // Null when the geocoder has nothing for that point and it is outside every partido/comuna.
        Task<DireccionDto?> GetDireccionPorUbicacionAsync(double latitud, double longitud, CancellationToken cancellationToken = default);
    }
}
