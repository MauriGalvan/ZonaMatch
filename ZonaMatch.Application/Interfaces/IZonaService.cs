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

        // OpenStreetMap points of interest of the given categories inside the zone, closest to its center first
        Task<PuntosInteresCercanosDto> GetPuntosInteresAsync(
            string slug, IReadOnlyCollection<string> categorias, CancellationToken cancellationToken = default);

        // Count of points of interest per category and type inside the zone (every category is listed)
        Task<ResumenPuntosInteresDto> GetResumenPuntosInteresAsync(string slug, CancellationToken cancellationToken = default);
    }
}
