using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Group;
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

        public static AnalisisDto ADto(Analisis analisis, IEnumerable<Analisis>? versiones = null) =>
            new(
                analisis.Id,
                analisis.Nombre,
                analisis.AnalisisOrigenId,
                analisis.Zonas
                    .OrderBy(z => z.Orden)
                    .Select(z => new ZonaAnalisisDto(z.ZonaSlug, z.Nombre, z.EsInicial, z.EsAgregada))
                    .ToList(),
                CriterioUsuario.ContextoADto(analisis.Contexto),
                CriterioUsuario.CriteriosADto(analisis.Contexto),
                CriterioUsuario.PuntosADto(analisis.Contexto),
                analisis.FechaCreacion,
                analisis.FechaActualizacion,
                analisis.PuntajesEnGrupoJson is null
                    ? null
                    : JsonSerializer.Deserialize<PuntajesEnGrupoResponse>(analisis.PuntajesEnGrupoJson, JsonOptions),
                (versiones ?? [])
                    .OrderBy(v => v.FechaCreacion)
                    .Select(v => ADto(v))
                    .ToList());

        // Zonas del pedido: existentes, sin repetir y con a lo sumo una inicial
        public static async Task<List<AnalisisZona>> ArmarZonasAsync(
            IReadOnlyList<ZonaAnalisisRequest> zonas, IZonaRepository repositorio, CancellationToken cancellationToken)
        {
            if (zonas.Count == 0)
                throw new DatosInvalidosException("Elegí al menos una zona.");

            if (zonas.Select(z => z.Slug).Distinct(StringComparer.Ordinal).Count() != zonas.Count)
                throw new DatosInvalidosException("Hay zonas repetidas en el análisis.");

            if (zonas.Count(z => z.EsInicial) > 1)
                throw new DatosInvalidosException("Solo puede haber una zona inicial.");

            foreach (var zona in zonas)
                await repositorio.ValidarExisteAsync(zona.Slug, cancellationToken);

            return zonas
                .Select((zona, indice) => new AnalisisZona
                {
                    Orden = indice + 1,
                    ZonaSlug = zona.Slug,
                    Nombre = zona.Nombre.Trim(),
                    EsInicial = zona.EsInicial,
                    EsAgregada = zona.EsAgregada,
                })
                .ToList();
        }
    }
}
