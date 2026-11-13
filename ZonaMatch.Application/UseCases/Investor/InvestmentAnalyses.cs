using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Investor.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Investor;

// Investment analyses are saved with the household ones, in "Mis análisis": each keeps its zones and a copy of the
// investor settings it was calculated with, so it can be adjusted without changing the investor profile. They are
// scored live when opened, with those settings.
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
}

public sealed class SaveInvestmentAnalysis(
    IUserRepository users,
    IAnalysisRepository analyses,
    ICriterionRepository criteria,
    IZoneRepository zones,
    TimeProvider clock)
{
    public async Task<AnalysisResponse> ExecuteAsync(
        Guid userId, SaveInvestmentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Poné un nombre para el análisis.");

        var analysis = await InvestmentAnalyses.BuildAsync(
            userId, request, request.Name.Trim(), criteria, zones, clock.GetUtcNow(), cancellationToken);
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
    TimeProvider clock)
{
    public async Task<AnalysisResponse> ExecuteAsync(
        Guid analysisId, Guid userId, SaveInvestmentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        await InvestorProfiles.EnsureAccessAsync(users, userId, cancellationToken);
        var source = await analyses.GetAsync(analysisId, userId, cancellationToken)
            ?? throw new NotFoundException("El análisis no existe.");
        if (source.Context.Type != ContextType.InvestorAnalysis)
            throw new BadRequestException("El análisis no es de inversión.");
        var original = source.OriginalAnalysisId is { } originalId
            ? await analyses.GetAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.")
            : source;

        var versions = await analyses.ListVersionsAsync(original.Id, cancellationToken);
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? CreateAnalysisVersion.BuildVersionName(original.Name, versions.Count + 2)
            : request.Name.Trim();
        var version = await InvestmentAnalyses.BuildAsync(userId, request, name, criteria, zones, clock.GetUtcNow(), cancellationToken);
        version.OriginalAnalysisId = original.Id;

        analyses.Add(version);
        await analyses.SaveChangesAsync(cancellationToken);

        return AnalysisMapping.ToResponse(version);
    }
}
