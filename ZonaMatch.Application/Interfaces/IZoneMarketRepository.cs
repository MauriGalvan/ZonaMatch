using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Real estate market per zone (app.zone_market), loaded by the ETL (etl/zonaprop/).
    public interface IZoneMarketRepository
    {
        // Every row of the zone (operation x property type); empty when there is no data for it
        Task<IReadOnlyList<ZoneMarket>> ListByZoneAsync(string slug, CancellationToken cancellationToken = default);

        // The comuna or partido the zone belongs to (slug and name), to compare with; null for comunas and partidos
        Task<(string Slug, string Name)?> GetParentAsync(string slug, CancellationToken cancellationToken = default);

        // Display name of a zone, for "estimated with the listings of ..."; null if the slug does not exist
        Task<string?> GetZoneNameAsync(string slug, CancellationToken cancellationToken = default);
    }
}
