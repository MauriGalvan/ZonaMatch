using ZonaMatch.Application.DTOs;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Scores a saved analysis with the data of today: its zones, criteria, places and limits.
    public interface IAnalysisResultCalculator
    {
        // Null when the zones cannot be scored (no data loaded for them): the analysis is saved without a result.
        Task<PuntajesDto?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default);
    }
}
