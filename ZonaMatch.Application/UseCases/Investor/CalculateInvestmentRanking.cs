using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Investor;

// Ranks barrios and localidades for the saved investor profile: the investment criteria with their priorities, and
// the capital as a purchase limit (against the sales eligible for a mortgage when the profile buys with one). Only
// zones with sale prices for the property type searched are ranked. Partidos, comunas and the city are left out:
// they add up the zones inside them.
public sealed class CalculateInvestmentRanking(
    IUserRepository users,
    IProfileRepository profiles,
    IZoneProfilesRepository zoneProfiles,
    MarketExchangeRate exchangeRate)
{
    public const int MaximumZones = 20;

    public async Task<InvestmentRankingResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        var profile = await profiles.GetInvestorProfileAsync(userId, cancellationToken)
            ?? throw new BadRequestException("Configurá tu perfil de inversor para ver el ranking.");

        var universe = await zoneProfiles.ListAsync(cancellationToken);
        var engine = new ScoringEngine(universe, exchangeRate.UsdArs);
        var request = new ScoringRequest(
            profile.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority),
            [],
            MaximumMonthlyBudget: null,
            HousingOperations.Purchase,
            profile.MaximumPurchasePriceUsd,
            profile.HousingType,
            AreaM2: null,
            profile.Rooms,
            profile.CreditEligible);

        var zones = universe
            .Where(zone => !zone.IsLargeZone && zone.GetMarket(profile.HousingType, profile.Rooms).AverageSalePriceUsd is not null)
            .Select(zone => (Zone: zone, Result: engine.Calculate(zone, request)))
            .Where(item => item.Result.Score is not null)
            .OrderByDescending(item => item.Result.Score)
            .ThenBy(item => item.Zone.Name, StringComparer.Ordinal)
            .Take(MaximumZones)
            .Select(item => ToResponse(item.Zone, item.Result, engine, profile.HousingType, profile.Rooms))
            .ToList();

        return new InvestmentRankingResponse(
            zones,
            profile.MaximumPurchasePriceUsd ?? 0,
            profile.HousingType,
            profile.Rooms,
            profile.CreditEligible,
            exchangeRate.UsdArs);
    }

    private static InvestmentZoneResponse ToResponse(
        ZoneProfile zone, ZoneScoreResult result, ScoringEngine engine, string? housingType, int? rooms)
    {
        var metrics = engine.MetricsFor(zone, housingType, rooms);
        var market = zone.GetMarket(housingType, rooms);
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
            HousingScoring.Sale(zone, housingType, rooms: rooms));
    }
}
