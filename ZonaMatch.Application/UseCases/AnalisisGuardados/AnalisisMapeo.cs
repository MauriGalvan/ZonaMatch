using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    public static class AnalisisMapeo
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static AnalysisResponse ToResponse(Analisis analisis, IEnumerable<Analisis>? versions = null) =>
            new(
                analisis.Id,
                analisis.Nombre,
                analisis.AnalisisOrigenId,
                analisis.Zonas
                    .OrderBy(z => z.Orden)
                    .Select(z => new AnalysisZoneResponse(
                        z.ZonaSlug, z.Nombre, z.EsInicial, z.EsAgregada,
                        z.TieneOrigen ? new AnalysisZoneOriginResponse(z.OrigenLatitud!.Value, z.OrigenLongitud!.Value) : null))
                    .ToList(),
                CriterioUsuario.ContextoADto(analisis.Contexto),
                CriterioUsuario.CriteriosADto(analisis.Contexto),
                CriterioUsuario.PuntosADto(analisis.Contexto),
                analisis.FechaCreacion,
                analisis.FechaActualizacion,
                analisis.PuntajesEnGrupoJson is null
                    ? null
                    : GroupRankingSnapshotJson.Deserialize(analisis.PuntajesEnGrupoJson, JsonOptions),
                (versions ?? [])
                    .OrderBy(v => v.FechaCreacion)
                    .Select(v => ToResponse(v))
                    .ToList(),
                AnalysisResultCalculator.Deserialize(analisis.ResultadoJson),
                analisis.FechaResultado,
                analisis.EsFavorito);

        // Request zones must exist, be unique, and include at most one initial zone.
        public static async Task<List<AnalisisZona>> ArmarZonasAsync(
            IReadOnlyList<AnalysisZoneRequest> zones, IZonaRepository repository, CancellationToken cancellationToken)
        {
            if (zones.Count == 0)
                throw new BadRequestException("Elegí al menos una zona.");

            if (zones.Select(zone => zone.Slug).Distinct(StringComparer.Ordinal).Count() != zones.Count)
                throw new BadRequestException("Hay zonas repetidas en el análisis.");

            if (zones.Count(zone => zone.IsInitial) > 1)
                throw new BadRequestException("Solo puede haber una zona inicial.");

            // A starting point replaces the center of the zone, so it has to be inside it
            foreach (var zone in zones)
            {
                if (zone.Origin is { } origin)
                    await repository.ValidarContienePuntoAsync(zone.Slug, origin.Latitude, origin.Longitude, cancellationToken);
                else
                    await repository.ValidarExisteAsync(zone.Slug, cancellationToken);
            }

            return zones
                .Select((zone, index) => new AnalisisZona
                {
                    Orden = index + 1,
                    ZonaSlug = zone.Slug,
                    Nombre = zone.Name.Trim(),
                    EsInicial = zone.IsInitial,
                    EsAgregada = zone.IsAdded,
                    OrigenLatitud = zone.Origin?.Latitude,
                    OrigenLongitud = zone.Origin?.Longitude,
                })
                .ToList();
        }
    }
}
