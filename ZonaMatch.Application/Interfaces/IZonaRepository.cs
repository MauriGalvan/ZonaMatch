namespace ZonaMatch.Application.Interfaces
{
    public interface IZonaRepository
    {
        // GeoJSON Feature (as JSON text) of the zone whose name matches the slug, or null if there is none.
        // "tolerancia" is the simplification tolerance of the boundary, in degrees.
        Task<string?> GetPorSlugAsync(string slug, double tolerancia, CancellationToken cancellationToken = default);
    }
}
