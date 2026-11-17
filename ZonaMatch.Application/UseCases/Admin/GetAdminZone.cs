using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Admin;

// One zone for the admin panel: the analysis breakdown by criterion, its market, and its credit-eligible sales.
public sealed class GetAdminZone(IZoneProfilesRepository zoneProfiles)
{
    // Segment order: every type first, then apartments, houses and PHs; every size first, then 1 to 5 rooms.
    private static readonly string[] TypeOrder = [HousingTypes.All, HousingTypes.Apartment, HousingTypes.House, HousingTypes.Ph];

    public async Task<AdminZoneDetailResponse> ExecuteAsync(string slug, CancellationToken cancellationToken = default)
    {
        var zones = await zoneProfiles.ListAsync(cancellationToken);
        var zone = zones.FirstOrDefault(item => item.Slug == slug)
            ?? throw new NotFoundException("No hay datos para la zona indicada.");

        var result = AdminZoneScoring.Score(new ScoringEngine(zones), zone);

        return new AdminZoneDetailResponse(
            zone.Slug,
            zone.Name,
            zone.Level,
            zone.IsLargeZone,
            zone.Population,
            zone.AreaKm2,
            result.Score,
            result.Components.Select(component => new AdminCriterionScoreResponse(component.Criterion, component.Score, component.Details)).ToList(),
            new AdminZoneMarketResponse(
                zone.AverageRent,
                zone.AvailableRentalProperties,
                zone.AverageExpenses,
                zone.RentPricePerM2,
                zone.AverageSalePriceUsd,
                zone.AvailablePropertiesForSale,
                zone.SalePricePerM2Usd),
            BuildCredit(zone));
    }

    private static AdminZoneCreditResponse? BuildCredit(ZoneProfile zone)
    {
        if (zone.CreditSalesBySegment.Count == 0)
            return null;

        var segments = zone.CreditSalesBySegment
            .Where(segment => segment.Value.HasData)
            .OrderBy(segment => Array.IndexOf(TypeOrder, segment.Key.Type) is var index and >= 0 ? index : TypeOrder.Length)
            .ThenBy(segment => segment.Key.Rooms)
            .Select(segment => new AdminCreditSegmentResponse(
                segment.Key.Type,
                segment.Key.Rooms,
                segment.Value.Listings,
                // The distribution adds a starting point at price 0; the cheapest listing is the first price read.
                segment.Value.Distribution is null ? null : segment.Value.PricesUsd![0],
                AdminZoneScoring.MedianPrice(segment.Value),
                segment.Value.Distribution?.Maximum))
            .ToList();

        return new AdminZoneCreditResponse(AdminZoneScoring.CreditListings(zone), segments);
    }
}
