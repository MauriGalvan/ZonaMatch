using System.Globalization;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Recalculates a saved analysis with today's data (rents, safety, transport, scoring rules) as a new version: same
    // criteria and zones as the analysis it starts from, the profile untouched, and the earlier result kept, so both
    // can be compared.
    public class RecalculateAnalysis(IAnalysisRepository analyses, IAnalysisResultCalculator calculator, TimeProvider clock)
    {
        private const int NameLength = 50;

        public async Task<AnalysisResponse> ExecuteAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default)
        {
            var source = await analyses.GetAsync(analysisId, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            // A group ranking mixes the profiles of several people: it is recalculated from the group
            if (source.GroupScoresJson is not null)
                throw new BadRequestException("Los análisis de grupo se recalculan desde el grupo.");

            // Versions always hang from the original, as the adjusted ones
            var originalId = source.OriginalAnalysisId ?? source.Id;
            var original = originalId == source.Id
                ? source
                : await analyses.GetAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.");

            var now = clock.GetUtcNow();
            var version = new Analysis
            {
                UserId = userId,
                Name = RecalculatedName(original.Name, now),
                Context = source.Context.CopiarParaAnalisis(now),
                OriginalAnalysisId = original.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Zones = source.Zones
                    .OrderBy(zone => zone.Order)
                    .Select(zone => zone.Copy())
                    .ToList(),
            };

            if (await calculator.CalculateAsync(version, cancellationToken) is { } result)
            {
                version.ResultJson = AnalysisResultCalculator.Serialize(result);
                version.ResultGeneratedAt = now;
            }

            analyses.Add(version);
            await analyses.SaveChangesAsync(cancellationToken);

            return AnalysisMapping.ToResponse(version);
        }

        // "Cerca del trabajo · datos al 25/10", trimmed to the maximum name length
        public static string RecalculatedName(string originalName, DateTimeOffset now)
        {
            var suffix = $" · datos al {now.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM", CultureInfo.InvariantCulture)}";
            var baseName = originalName.Length + suffix.Length > NameLength
                ? originalName[..(NameLength - suffix.Length)].TrimEnd()
                : originalName;

            return baseName + suffix;
        }
    }
}
