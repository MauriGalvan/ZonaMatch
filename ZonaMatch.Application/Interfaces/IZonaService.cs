using ZonaMatch.Application.DTOs;

namespace ZonaMatch.Application.Interfaces
{
    public interface IZonaService
    {
        // GeoJSON Feature of the zone, or null if the slug matches none
        Task<string?> GetPorSlugAsync(string slug, double? tolerancia, CancellationToken cancellationToken = default);

        // Zones whose name contains the text, ignoring case and accents
        Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(
            string texto, int limite, CancellationToken cancellationToken = default);
    }
}
