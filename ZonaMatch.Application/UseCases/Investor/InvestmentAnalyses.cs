using System.Text.Json;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.DTOs.Investor.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Investor;

// Investment analyses are saved with the household ones, in "Mis análisis", and work like them: each keeps its zones,
// a copy of the investor settings it was calculated with (so it can be adjusted without changing the investor profile)
// and the ranking as it was when saved, shown when it is opened again. Adjusting it or recalculating it with current
// data saves a new version.
public static class InvestmentAnalyses
{
    public static async Task<Analysis> BuildAsync(
        Guid userId,
        SaveInvestmentAnalysisRequest request,
        string name,
        ICriterionRepository criteria,
        IZoneRepository zones,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.Zones.Count > CalculateInvestmentRanking.MaximumZones)
            throw new BadRequestException($"El análisis puede tener hasta {CalculateInvestmentRanking.MaximumZones} zonas.");

        var context = new Context { UserId = userId, Type = ContextType.InvestorAnalysis, CreatedAt = now, UpdatedAt = now };
        InvestorProfiles.Apply(context, request.Settings, await criteria.ListAsync(cancellationToken));

        return new Analysis
        {
            UserId = userId,
            Name = name,
            Context = context,
            CreatedAt = now,
            UpdatedAt = now,
            Zones = await AnalysisMapping.BuildZonesAsync(request.Zones, zones, cancellationToken),
        };
    }

    // The settings saved with an analysis, as the ranking request takes them
    public static InvestorProfileRequest SettingsOf(Context context) => new()
    {
        Criteria = context.Criteria.ToDictionary(criterion => criterion.CriterionId, criterion => (string?)criterion.Priority),
        CapitalUsd = context.MaximumPurchasePriceUsd,
        CreditEligible = context.CreditEligible,
        PropertyType = context.HousingType,
        Rooms = context.Rooms,
    };

    // Saves the ranking of the analysis zones with its settings and today's data. A zone without data does not block
    // saving: the analysis is then scored live when opened, like the household ones saved before results were kept.
    public static async Task KeepResultAsync(
        Analysis analysis, CalculateInvestmentRanking ranking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ranking.ExecuteAsync(
                analysis.UserId,
                new InvestmentRankingRequest
                {
                    Settings = SettingsOf(analysis.Context),
                    Zones = analysis.Zones.OrderBy(zone => zone.Order).Select(zone => zone.ZoneSlug).ToList(),
                },
                cancellationToken);
            analysis.ResultJson = JsonSerializer.Serialize(result, AnalysisResultCalculator.JsonOptions);
            analysis.ResultGeneratedAt = now;
        }
        catch (Exception ex) when (ex is NotFoundException or BadRequestException)
        {
        }
    }

    public static InvestmentRankingResponse? DeserializeResult(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<InvestmentRankingResponse>(json, AnalysisResultCalculator.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The analysis and the original its versions hang from, which must be an investment analysis of the user
    public static async Task<(Analysis Source, Analysis Original)> FindAsync(
        IAnalysisRepository analyses, Guid analysisId, Guid userId, CancellationToken cancellationToken)
    {
        var source = await analyses.GetAsync(analysisId, userId, cancellationToken)
            ?? throw new NotFoundException("El análisis no existe.");
        if (source.Context?.Type != ContextType.InvestorAnalysis)
            throw new BadRequestException("El análisis no es de inversión.");
        var original = source.OriginalAnalysisId is { } originalId
            ? await analyses.GetAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.")
            : source;
        return (source, original);
    }
}

public sealed class SaveInvestmentAnalysis(
    IUserRepository users,
    IAnalysisRepository analyses,
    ICriterionRepository criteria,
    IZoneRepository zones,
    CalculateInvestmentRanking ranking,
    TimeProvider clock)
{
    public async Task<AnalysisResponse> ExecuteAsync(
        Guid userId, SaveInvestmentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Poné un nombre para el análisis.");

        var now = clock.GetUtcNow();
        var analysis = await InvestmentAnalyses.BuildAsync(userId, request, request.Name.Trim(), criteria, zones, now, cancellationToken);
        await InvestmentAnalyses.KeepResultAsync(analysis, ranking, now, cancellationToken);
        analyses.Add(analysis);
        await analyses.SaveChangesAsync(cancellationToken);

        return AnalysisMapping.ToResponse(analysis);
    }
}

// Adjusts the settings or zones of a saved investment analysis into a new version, as with the household analyses:
// versions always belong to the original, and the original keeps its own settings.
public sealed class CreateInvestmentAnalysisVersion(
    IUserRepository users,
    IAnalysisRepository analyses,
    ICriterionRepository criteria,
    IZoneRepository zones,
    CalculateInvestmentRanking ranking,
    TimeProvider clock)
{
    public async Task<AnalysisResponse> ExecuteAsync(
        Guid analysisId, Guid userId, SaveInvestmentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        var (_, original) = await InvestmentAnalyses.FindAsync(analyses, analysisId, userId, cancellationToken);

        var versions = await analyses.ListVersionsAsync(original.Id, cancellationToken);
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? CreateAnalysisVersion.BuildVersionName(original.Name, versions.Count + 2)
            : request.Name.Trim();
        var now = clock.GetUtcNow();
        var version = await InvestmentAnalyses.BuildAsync(userId, request, name, criteria, zones, now, cancellationToken);
        version.OriginalAnalysisId = original.Id;
        await InvestmentAnalyses.KeepResultAsync(version, ranking, now, cancellationToken);

        analyses.Add(version);
        await analyses.SaveChangesAsync(cancellationToken);

        return AnalysisMapping.ToResponse(version);
    }
}

// "Recalcular con datos actuales", as with the household analyses: the same settings and zones scored with today's
// data, saved as a new version so the previous result is kept.
public sealed class RecalculateInvestmentAnalysis(
    IUserRepository users,
    IAnalysisRepository analyses,
    CalculateInvestmentRanking ranking,
    TimeProvider clock)
{
    public async Task<AnalysisResponse> ExecuteAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        var (source, original) = await InvestmentAnalyses.FindAsync(analyses, analysisId, userId, cancellationToken);

        var now = clock.GetUtcNow();
        var context = new Context { UserId = userId, Type = ContextType.InvestorAnalysis, CreatedAt = now, UpdatedAt = now };
        context.Operation = source.Context.Operation;
        context.MaximumPurchasePriceUsd = source.Context.MaximumPurchasePriceUsd;
        context.CreditEligible = source.Context.CreditEligible;
        context.HousingType = source.Context.HousingType;
        context.Rooms = source.Context.Rooms;
        context.PeopleCount = source.Context.PeopleCount;
        foreach (var criterion in source.Context.Criteria)
            context.Criteria.Add(new ContextCriterion { CriterionId = criterion.CriterionId, Priority = criterion.Priority });

        var version = new Analysis
        {
            UserId = userId,
            Name = RecalculateAnalysis.RecalculatedName(original.Name, now),
            Context = context,
            OriginalAnalysisId = original.Id,
            CreatedAt = now,
            UpdatedAt = now,
            Zones = source.Zones.OrderBy(zone => zone.Order).Select(zone => zone.Copy()).ToList(),
        };
        await InvestmentAnalyses.KeepResultAsync(version, ranking, now, cancellationToken);

        analyses.Add(version);
        await analyses.SaveChangesAsync(cancellationToken);

        return AnalysisMapping.ToResponse(version);
    }
}
