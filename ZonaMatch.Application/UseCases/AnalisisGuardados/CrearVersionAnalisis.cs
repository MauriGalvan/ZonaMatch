using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    // Adjusts criteria for one analysis and saves a derived version; the original and the profile stay unchanged.
    // Versions always belong to the original, even when another version was adjusted.
    public class CrearVersionAnalisis
    {
        private const int LargoNombre = 50;

        private readonly IAnalisisRepository _analisis;
        private readonly ICriterioRepository _criterios;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _reloj;

        public CrearVersionAnalisis(
            IAnalisisRepository analisis, ICriterioRepository criterios, IZonaRepository zonas, TimeProvider reloj)
        {
            _analisis = analisis;
            _criterios = criterios;
            _zonas = zonas;
            _reloj = reloj;
        }

        public async Task<AnalysisResponse> EjecutarAsync(
            Guid analisisId, Guid usuarioId, CreateAnalysisVersionRequest solicitud, CancellationToken cancellationToken = default)
        {
            var pedido = await _analisis.ObtenerAsync(analisisId, usuarioId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var original = pedido.AnalisisOrigenId is { } origenId
                ? await _analisis.ObtenerAsync(origenId, usuarioId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.")
                : pedido;

            var catalogo = await _criterios.ListarAsync(cancellationToken);
            var ahora = _reloj.GetUtcNow();

            var contexto = new Contexto
            {
                UsuarioId = usuarioId,
                Tipo = TipoContexto.Analisis,
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            };
            CriterioUsuario.Aplicar(contexto, solicitud.Criterion, catalogo);

            var zonas = solicitud.Zones is { } requestedZones
                ? await AnalisisMapeo.ArmarZonasAsync(requestedZones, _zonas, cancellationToken)
                : pedido.Zonas
                    .OrderBy(z => z.Orden)
                    .Select(z => new AnalisisZona
                    {
                        Orden = z.Orden,
                        ZonaSlug = z.ZonaSlug,
                        Nombre = z.Nombre,
                        EsInicial = z.EsInicial,
                        EsAgregada = z.EsAgregada,
                    })
                    .ToList();

            var versiones = await _analisis.ListarVersionesAsync(original.Id, cancellationToken);

            var version = new Analisis
            {
                UsuarioId = usuarioId,
                Nombre = string.IsNullOrWhiteSpace(solicitud.Name)
                    ? NombreDeVersion(original.Nombre, versiones.Count + 2)
                    : solicitud.Name.Trim(),
                Contexto = contexto,
                AnalisisOrigenId = original.Id,
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
                Zonas = zonas,
            };

            _analisis.Agregar(version);
            await _analisis.GuardarCambiosAsync(cancellationToken);

            return AnalisisMapeo.ToResponse(version);
        }

        // "Near work (2)": the original counts as version 1; trim the name to stay within the maximum length.
        public static string NombreDeVersion(string nombreOriginal, int numero)
        {
            var sufijo = $" ({numero})";
            var baseNombre = nombreOriginal.Length + sufijo.Length > LargoNombre
                ? nombreOriginal[..(LargoNombre - sufijo.Length)].TrimEnd()
                : nombreOriginal;

            return baseNombre + sufijo;
        }
    }
}
