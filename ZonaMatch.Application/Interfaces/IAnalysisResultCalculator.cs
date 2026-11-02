using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Scores a saved analysis with the data of today: its zones, criteria, places and limits.
    public interface IAnalysisResultCalculator
    {
        // Null when the zones cannot be scored (no data loaded for them): the analysis is saved without a result.
        Task<ScoresResponse?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default);
    }
}
