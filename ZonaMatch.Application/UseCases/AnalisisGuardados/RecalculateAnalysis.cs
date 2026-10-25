using System.Globalization;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    // Recalculates a saved analysis with today's data (rents, safety, transport, scoring rules) as a new version: same
    // criteria and zones as the analysis it starts from, the profile untouched, and the earlier result kept, so both
    // can be compared.
    public class RecalculateAnalysis(IAnalisisRepository analyses, IAnalysisResultCalculator calculator, TimeProvider clock)
    {
        private const int NameLength = 50;

        public async Task<AnalysisResponse> ExecuteAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default)
        {
            var source = await analyses.ObtenerAsync(analysisId, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            // A group ranking mixes the profiles of several people: it is recalculated from the group
            if (source.PuntajesEnGrupoJson is not null)
                throw new BadRequestException("Los análisis de grupo se recalculan desde el grupo.");

            // Versions always hang from the original, as the adjusted ones
            var originalId = source.AnalisisOrigenId ?? source.Id;
            var original = originalId == source.Id
                ? source
                : await analyses.ObtenerAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.");

            var now = clock.GetUtcNow();
            var version = new Analisis
            {
                UsuarioId = userId,
                Nombre = RecalculatedName(original.Nombre, now),
                Contexto = source.Contexto.CopiarParaAnalisis(now),
                AnalisisOrigenId = original.Id,
                FechaCreacion = now,
                FechaActualizacion = now,
                Zonas = source.Zonas
                    .OrderBy(zone => zone.Orden)
                    .Select(zone => new AnalisisZona
                    {
                        Orden = zone.Orden,
                        ZonaSlug = zone.ZonaSlug,
                        Nombre = zone.Nombre,
                        EsInicial = zone.EsInicial,
                        EsAgregada = zone.EsAgregada,
                    })
                    .ToList(),
            };

            if (await calculator.CalculateAsync(version, cancellationToken) is { } result)
            {
                version.ResultadoJson = AnalysisResultCalculator.Serialize(result);
                version.FechaResultado = now;
            }

            analyses.Agregar(version);
            await analyses.GuardarCambiosAsync(cancellationToken);

            return AnalisisMapeo.ToResponse(version);
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
