using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Lists each original analysis with its versions, ordered by most recent activity.
    // Version activity contributes to the position of its original analysis.
    public class ListMyAnalyses
    {
        private readonly IAnalysisRepository _analysisRepository;

        public ListMyAnalyses(IAnalysisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository;
        }

        public async Task<IReadOnlyList<AnalysisResponse>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var allAnalyses = await _analysisRepository.GetForUserAsync(userId, cancellationToken);
            var versionsByOriginalId = allAnalyses
                .Where(a => a.OriginalAnalysisId is not null)
                .ToLookup(a => a.OriginalAnalysisId!.Value);

            return allAnalyses
                .Where(a => a.OriginalAnalysisId is null)
                .Select(original => new
                {
                    Original = original,
                    Versions = versionsByOriginalId[original.Id].ToList(),
                })
                .OrderByDescending(x => x.Versions
                    .Select(v => v.UpdatedAt)
                    .Append(x.Original.UpdatedAt)
                    .Max())
                .Select(x => AnalysisMapping.ToResponse(x.Original, x.Versions))
                .ToList();
        }
    }
}
