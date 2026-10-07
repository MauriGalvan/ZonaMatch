using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IZonaRepository
    {
        // GeoJSON Feature (as JSON text) of the zone whose name matches the slug, or null if there is none.
        // "tolerancia" is the simplification tolerance of the boundary, in degrees.
        Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default);

        Task<bool> ExisteAsync(string slug, CancellationToken cancellationToken = default);

        // Whether the point (WGS84) falls inside the zone; false when the zone does not exist
        Task<bool> ContienePuntoAsync(string slug, double latitud, double longitud, CancellationToken cancellationToken = default);

        // Zones whose slug contains "fragmento" (a slug itself, e.g. "villa-l"); names that start with it come first
        Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(
            string fragmento, int limite, CancellationToken cancellationToken = default);
    }
}
