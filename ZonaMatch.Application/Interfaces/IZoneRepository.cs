using ZonaMatch.Application.DTOs.Zones.Responses;

namespace ZonaMatch.Application.Interfaces
{
    public interface IZoneRepository
    {
        // GeoJSON Feature (as JSON text) of the zone whose name matches the slug, or null if there is none.
        // "tolerancia" is the simplification tolerance of the boundary, in degrees.
        Task<string?> GetBySlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default);

        // Si el punto (WGS84) cae dentro de la zona; false si la zona no existe
        Task<bool> ContainsPointAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default);

        // Zones whose slug contains "fragmento" (a slug itself, e.g. "villa-l"); names that start with it come first
        Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(
            string fragmento, int limite, CancellationToken cancellationToken = default);
    }
}
