using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Lists each original analysis with its versions, ordered by most recent activity.
    // Version activity contributes to the position of its original analysis.
    public class ListMyAnalyses
    {
        private readonly IAnalisisRepository _analysisRepository;

        public ListMyAnalyses(IAnalisisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository;
        }

        public async Task<IReadOnlyList<AnalysisResponse>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var allAnalyses = await _analysisRepository.ListarDeUsuarioAsync(userId, cancellationToken);
            var versionsByOriginalId = allAnalyses
                .Where(a => a.AnalisisOrigenId is not null)
                .ToLookup(a => a.AnalisisOrigenId!.Value);

            return allAnalyses
                .Where(a => a.AnalisisOrigenId is null)
                .Select(original => new
                {
                    Original = original,
                    Versiones = versionsByOriginalId[original.Id].ToList(),
                })
                .OrderByDescending(x => x.Versiones
                    .Select(v => v.FechaActualizacion)
                    .Append(x.Original.FechaActualizacion)
                    .Max())
                .Select(x => AnalysisMapping.ToResponse(x.Original, x.Versiones))
                .ToList();
        }
    }
}
