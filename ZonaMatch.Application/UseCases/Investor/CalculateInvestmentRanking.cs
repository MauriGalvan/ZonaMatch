using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Investor;

// Ranks barrios and localidades for an investment analysis: the investment criteria with their priorities, and the
// capital as a purchase limit (against the sales eligible for a mortgage when buying with one). Like the household
// analysis, it shows the best InitialZones, the zones the user added up to MaximumZones, and the next best as
// recommendations. Only zones with listings of their own for the property type and size searched enter the automatic
// ranking, at least MinimumSaleListings: zones that inherit the prices of the zone around them would repeat its figures,
// and a handful of listings gives no reliable yield. Partidos, comunas and the city are left out: they add up the zones
// inside them. A zone the user adds is scored whatever its listings.
public sealed class CalculateInvestmentRanking(
    IUserRepository users,
    IProfileRepository profiles,
    ICriterionRepository criteria,
    IZoneProfilesRepository zoneProfiles,
    MarketExchangeRate exchangeRate)
{
    public const int InitialZones = 5;
    public const int MaximumZones = 10;
    public const int RecommendationsReturned = 4;
    public const int MinimumSaleListings = 10;

    public async Task<InvestmentRankingResponse> ExecuteAsync(
        Guid userId, InvestmentRankingRequest? request = null, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        var settings = await ResolveSettingsAsync(userId, request?.Settings, cancellationToken);

        var universe = await zoneProfiles.ListAsync(cancellationToken);
        var zonesBySlug = universe.ToDictionary(zone => zone.Slug, StringComparer.Ordinal);
        ZoneProfile Find(string slug) =>
            zonesBySlug.TryGetValue(slug.Trim(), out var zone) ? zone : throw new NotFoundException($"No hay datos para la zona {slug}.");
        var fixedZones = request?.Zones?.Distinct(StringComparer.Ordinal).Select(Find).ToList();
        var added = (request?.AddedZones ?? []).Distinct(StringComparer.Ordinal).Select(Find).ToList();

        var engine = new ScoringEngine(universe, exchangeRate.UsdArs);
        var scoringRequest = new ScoringRequest(
            settings.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority),
            [],
            MaximumMonthlyBudget: null,
            HousingOperations.Purchase,
            settings.MaximumPurchasePriceUsd,
            settings.HousingType,
            AreaM2: null,
            settings.Rooms,
            settings.CreditEligible);

        var ranking = universe
            .Where(zone => !zone.IsLargeZone && HasOwnSales(zone.GetMarket(settings.HousingType, settings.Rooms)))
            .Select(zone => engine.Calculate(zone, scoringRequest))
            .Where(result => result.Score is not null)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Zone.Name, StringComparer.Ordinal)
            .ToList();

        // A saved analysis keeps its zones; a new one starts from the automatic top
        var top = fixedZones is null ? ranking.Take(InitialZones).ToList() : [];
        var extra = (fixedZones ?? []).Concat(added)
            .DistinctBy(zone => zone.Slug, StringComparer.Ordinal)
            .Where(zone => top.All(result => result.Zone.Slug != zone.Slug))
            .Select(zone => ranking.FirstOrDefault(result => result.Zone.Slug == zone.Slug) ?? engine.Calculate(zone, scoringRequest))
            .ToList();
        if (top.Count + extra.Count > MaximumZones)
            throw new BadRequestException($"El análisis puede tener hasta {MaximumZones} zonas.");

        var included = top.Concat(extra)
            .OrderByDescending(result => result.Score.HasValue)
            .ThenByDescending(result => result.Score)
            .ThenBy(result => result.Zone.Name, StringComparer.Ordinal)
            .ToList();
        var includedSlugs = included.Select(result => result.Zone.Slug).ToHashSet(StringComparer.Ordinal);
        var recommendations = ranking
            .Where(result => !includedSlugs.Contains(result.Zone.Slug))
            .Take(RecommendationsReturned);

        return new InvestmentRankingResponse(
            included.Select(result => ToResponse(result, engine, settings)).ToList(),
            recommendations.Select(result => ToResponse(result, engine, settings)).ToList(),
            settings.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority),
            settings.MaximumPurchasePriceUsd ?? 0,
            settings.HousingType,
            settings.Rooms,
            settings.CreditEligible,
            exchangeRate.UsdArs);
    }

    // The settings sent for one analysis, validated like the profile, or else the saved investor profile
    private async Task<Context> ResolveSettingsAsync(Guid userId, InvestorProfileRequest? settings, CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            return await profiles.GetInvestorProfileAsync(userId, cancellationToken)
                ?? throw new BadRequestException("Configurá tu perfil de inversor para ver el ranking.");
        }

        var context = new Context { UserId = userId, Type = ContextType.InvestorAnalysis };
        InvestorProfiles.Apply(context, settings, await criteria.ListAsync(cancellationToken));
        return context;
    }

    private static bool HasOwnSales(HousingMarket market) =>
        market.AverageSalePriceUsd is not null && market.AvailablePropertiesForSale >= MinimumSaleListings;

    private static InvestmentZoneResponse ToResponse(ZoneScoreResult result, ScoringEngine engine, Context settings)
    {
        var zone = result.Zone;
        var metrics = engine.MetricsFor(zone, settings.HousingType, settings.Rooms);
        var market = zone.GetMarket(settings.HousingType, settings.Rooms);
        return new InvestmentZoneResponse(
            zone.Slug,
            zone.Name,
            result.Score,
            new InvestmentMetricsResponse(
                metrics.GrossYieldPercent,
                metrics.PaybackYears,
                metrics.PriceBelowParentPercent,
                metrics.ParentName,
                metrics.SaleListings,
                metrics.SaleListingsPerThousandResidents,
                metrics.RentingHouseholdsPercentage,
                market.AverageRent,
                market.AverageSalePriceUsd),
            result.Components.Select(c => new ScoreComponentResponse(c.Criterion, c.Priority, c.Score, c.Details)).ToList(),
            result.Limits.Select(l => new ScoreLimitResponse(l.Type, l.IsSatisfied, l.Details)).ToList(),
            HousingScoring.Sale(zone, settings.HousingType, rooms: settings.Rooms));
    }
}
