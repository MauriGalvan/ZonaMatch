using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IGeolocationService
    {
        // Resumen completo (barrio, zona, escuelas y espacios verdes cercanos) para una ubicacion
        Task<UbicacionResumenDto> GetResumenUbicacionAsync(double latitud, double longitud, double radioMetros);

        Task<IReadOnlyList<EscuelaDto>> GetEscuelasCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel = null, string? gestion = null);

        Task<IReadOnlyList<EspacioVerdeDto>> GetEspaciosVerdesCercanosAsync(double latitud, double longitud, double radioMetros);

        Task<BarrioDto?> GetBarrioPorUbicacionAsync(double latitud, double longitud);

        Task<ZonaDto?> GetZonaPorUbicacionAsync(double latitud, double longitud);
    }
}
