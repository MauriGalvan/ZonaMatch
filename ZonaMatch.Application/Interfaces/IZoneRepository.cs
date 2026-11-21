using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IZoneRepository
    {
        // The zone for the slug with its boundary simplified by the tolerance (in degrees), or null if there is none.
        Task<Zone?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default);

        // Returns whether the WGS84 point is inside the zone; false if the zone does not exist.
        Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default);

        // Zones whose slug contains the search text turned into a slug with the same rule the ETL uses for the zone
        // slugs (geo.slug_zona); prefix matches come first. Results come without their boundary.
        Task<IReadOnlyList<Zone>> SearchAsync(string searchText, int resultLimit, CancellationToken cancellationToken = default);

        // Active coverage zones with simplified boundaries as a GeoJSON FeatureCollection string.
        Task<string> GetCoverageGeoJsonAsync(double simplificationTolerance, CancellationToken cancellationToken = default);

        // Lightweight list of active zones and indicator availability without geometries.
        Task<IReadOnlyList<AvailableZoneResponse>> ListAvailableAsync(CancellationToken cancellationToken = default);
    }
}
