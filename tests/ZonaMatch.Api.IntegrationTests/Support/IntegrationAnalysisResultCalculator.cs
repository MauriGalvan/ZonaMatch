using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class IntegrationAnalysisResultCalculator : IAnalysisResultCalculator
{
    public Task<ScoresResponse?> CalculateAsync(
        Analysis analysis,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ScoresResponse?>(null);
}
