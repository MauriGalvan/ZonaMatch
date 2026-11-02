using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Groups;

public sealed class SaveGroupAnalysis
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CalculateGroupScore _calculateRanking;
    private readonly IAnalysisRepository _analyses;
    private readonly IProfileRepository _profiles;
    private readonly IZoneRepository _zones;
    private readonly TimeProvider _clock;

    public SaveGroupAnalysis(
        CalculateGroupScore calculateRanking,
        IAnalysisRepository analyses,
        IProfileRepository profiles,
        IZoneRepository zones,
        TimeProvider clock)
    {
        _calculateRanking = calculateRanking;
        _analyses = analyses;
        _profiles = profiles;
        _zones = zones;
        _clock = clock;
    }

    public async Task<AnalysisResponse> ExecuteAsync(
        Guid groupId,
        Guid currentUserId,
        SaveGroupAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            throw new BadRequestException("El nombre es obligatorio.");

        var now = _clock.GetUtcNow();
        var ranked = await _calculateRanking.ExecuteAsync(
            groupId,
            currentUserId,
            new CalculateGroupScoreRequest
            {
                AddedZones = request.AddedZones ?? [],
                ParticipantWeights = request.ParticipantWeights,
                MaximumRent = request.MaximumRent,
                Operation = request.Operation,
                MaximumPurchaseUsd = request.MaximumPurchaseUsd,
                PropertyType = request.PropertyType,
                SurfaceM2 = request.SurfaceM2,
                Rooms = request.Rooms,
            },
            cancellationToken);

        var profile = await _profiles.GetByUserAsync(currentUserId, cancellationToken);
        var context = profile?.CopyForAnalysis(now) ?? new Context
        {
            UserId = currentUserId,
            Type = ContextType.Analysis,
            PeopleCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var zones = await AnalysisMapping.BuildZonesAsync(request.Zones, _zones, cancellationToken);

        var analysis = new Analysis
        {
            UserId = currentUserId,
            Name = name,
            Context = context,
            CreatedAt = now,
            UpdatedAt = now,
            Zones = zones,
            GroupScoresJson = JsonSerializer.Serialize(ranked, JsonOptions),
        };

        _analyses.Add(analysis);
        await _analyses.SaveChangesAsync(cancellationToken);

        return AnalysisMapping.ToResponse(analysis);
    }
}
