namespace ZonaMatch.Application.Interfaces
{
    public interface IZonaService
    {
        // GeoJSON Feature of the zone, or null if the slug matches none
        Task<string?> GetPorSlugAsync(string slug, double? tolerancia, CancellationToken cancellationToken = default);
    }
}
