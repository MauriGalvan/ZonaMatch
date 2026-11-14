using System.Globalization;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Admin;

// Every zone with data, sorted by name, and how many of them have each kind of data.
public sealed class ListAdminZones(IZoneProfilesRepository zoneProfiles)
{
    private static readonly StringComparer SpanishOrder = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), ignoreCase: true);

    public async Task<AdminZoneOverviewResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var zones = await zoneProfiles.ListAsync(cancellationToken);
        var engine = new ScoringEngine(zones);

        var coverage = new AdminZoneCoverage(
            Zones: zones.Count,
            SmallZones: zones.Count(zone => !zone.IsLargeZone),
            LargeZones: zones.Count(zone => zone.IsLargeZone),
            WithSafety: zones.Count(zone => zone.CrimesPerThousandResidents is not null),
            WithRent: zones.Count(zone => zone.AverageRent is not null),
            WithSale: zones.Count(zone => zone.AverageSalePriceUsd is not null),
            WithCreditSales: zones.Count(zone => AdminZoneScoring.CreditListings(zone) > 0));

        var summaries = zones
            .OrderBy(zone => zone.Name, SpanishOrder)
            .ThenBy(zone => zone.Slug, StringComparer.Ordinal)
            .Select(zone => new AdminZoneSummaryResponse(
                zone.Slug,
                zone.Name,
                zone.Level,
                zone.IsLargeZone,
                zone.Population,
                AdminZoneScoring.Score(engine, zone).Score,
                zone.AverageRent,
                zone.AverageSalePriceUsd,
                AdminZoneScoring.CreditListings(zone)))
            .ToList();

        return new AdminZoneOverviewResponse(coverage, summaries);
    }
}
