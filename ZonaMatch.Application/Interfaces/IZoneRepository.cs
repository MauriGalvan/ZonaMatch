using ZonaMatch.Application.DTOs.Zone.Responses;

namespace ZonaMatch.Application.Interfaces
{
    public interface IZoneRepository
    {
        // GeoJSON Feature (as JSON text) of the zone whose name matches the slug, or null if there is none.
        // Simplification tolerance for the boundary, in degrees.
        Task<string?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default);

        // Returns whether the WGS84 point is inside the zone; false if the zone does not exist.
        Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default);

        // Zones whose slug contains the search fragment (a slug itself, e.g. "villa-l"); prefix matches come first.
        Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(
            string searchFragment, int resultLimit, CancellationToken cancellationToken = default);
    }
}
