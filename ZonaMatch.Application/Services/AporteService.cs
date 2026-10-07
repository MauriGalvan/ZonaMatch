using NetTopologySuite.Geometries;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class AporteService : IAporteService
    {
        private readonly IAporteRepository _aportes;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _timeProvider;

        public AporteService(IAporteRepository aportes, IZonaRepository zonas, TimeProvider timeProvider)
        {
            _aportes = aportes;
            _zonas = zonas;
            _timeProvider = timeProvider;
        }

        public async Task<ResumenAportesDto> ResumirAsync(string zonaSlug, CancellationToken cancellationToken = default)
        {
            var (aprobados, pendientes) = await _aportes.ContarAsync(zonaSlug, cancellationToken);
            return new ResumenAportesDto(aprobados + pendientes, aprobados, pendientes);
        }

        public async Task<IReadOnlyList<AporteDto>> ListarPendientesAsync(
            string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default)
        {
            var aportes = await _aportes.ListarPendientesAsync(zonaSlug, cancellationToken);
            return aportes.Select(a => ADto(a, a.Usuario?.Email, usuarioActual)).ToList();
        }

        public async Task<AporteDto> CrearPuntoAsync(
            string zonaSlug, SesionDto usuario, CrearPuntoNuevoDto request, CancellationToken cancellationToken = default)
        {
            var categoria = ValidarCategoria(request.Categoria);
            await ValidarDentroDeZonaAsync(zonaSlug, request.Latitud, request.Longitud, cancellationToken);

            var aporte = new Aporte
            {
                ZonaSlug = zonaSlug,
                UsuarioId = usuario.Id,
                Tipo = TipoAporte.PuntoNuevo,
                Categoria = categoria,
                Nombre = request.Nombre.Trim(),
                Ubicacion = Punto(request.Latitud, request.Longitud),
                Horario = Limpiar(request.Horario),
                Direccion = Limpiar(request.Direccion),
                ViveOTrabajaEnZona = request.ViveOTrabajaEnZona,
                FechaCreacion = _timeProvider.GetUtcNow()
            };

            _aportes.Agregar(aporte);
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return ADto(aporte, usuario.Email, usuario.Id);
        }

        public async Task<AporteDto> CrearCorreccionAsync(
            string zonaSlug, SesionDto usuario, CrearCorreccionDto request, CancellationToken cancellationToken = default)
        {
            var motivo = request.Motivo!.Value; // guaranteed by [Required] on the DTO

            var aporte = new Aporte
            {
                ZonaSlug = zonaSlug,
                UsuarioId = usuario.Id,
                Tipo = TipoAporte.Correccion,
                PuntoInteresId = request.PuntoInteresId,
                PuntoInteresNombre = request.PuntoInteresNombre.Trim(),
                Motivo = motivo,
                Comentario = Limpiar(request.Comentario),
                FechaCreacion = _timeProvider.GetUtcNow()
            };

            // Only the value that matches the reason is kept
            switch (motivo)
            {
                case MotivoCorreccion.Nombre:
                    aporte.Nombre = Limpiar(request.NombrePropuesto)
                        ?? throw new DatosInvalidosException("Indica el nombre correcto del lugar.");
                    break;

                case MotivoCorreccion.Categoria:
                    aporte.Categoria = ValidarCategoria(request.CategoriaPropuesta);
                    break;

                case MotivoCorreccion.Ubicacion:
                    if (request.Latitud is not { } latitud || request.Longitud is not { } longitud)
                        throw new DatosInvalidosException("Marca en el mapa la ubicacion correcta del lugar.");
                    await ValidarDentroDeZonaAsync(zonaSlug, latitud, longitud, cancellationToken);
                    aporte.Ubicacion = Punto(latitud, longitud);
                    break;

                case MotivoCorreccion.Otro when aporte.Comentario is null:
                    throw new DatosInvalidosException("Contanos que hay que corregir en el comentario.");
            }

            // The location check above already covers the zone; the other reasons check it here
            if (motivo != MotivoCorreccion.Ubicacion && !await _zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NoEncontradoException("No se encontro una zona con ese identificador.");

            _aportes.Agregar(aporte);
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return ADto(aporte, usuario.Email, usuario.Id);
        }

        public async Task<AporteDto> ValidarAsync(
            string zonaSlug, Guid aporteId, Guid usuarioId, bool confirma, CancellationToken cancellationToken = default)
        {
            var aporte = await _aportes.ObtenerAsync(zonaSlug, aporteId, cancellationToken)
                ?? throw new NoEncontradoException("El aporte no existe.");

            if (aporte.Estado != EstadoAporte.Pendiente)
                throw new OperacionNoPermitidaException("El aporte ya fue resuelto por otros vecinos.");

            if (aporte.UsuarioId == usuarioId)
                throw new OperacionNoPermitidaException("No podes validar tu propio aporte.");

            aporte.Validar(usuarioId, confirma, _timeProvider.GetUtcNow());
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return ADto(aporte, aporte.Usuario?.Email, usuarioId);
        }

        private static string ValidarCategoria(string? categoria)
        {
            var codigo = categoria?.Trim().ToLowerInvariant();
            if (codigo is null || !CategoriaPuntoInteres.Todas.Contains(codigo))
                throw new DatosInvalidosException(
                    $"Categoria desconocida. Validas: {string.Join(", ", CategoriaPuntoInteres.Todas)}.");

            return codigo;
        }

        private async Task ValidarDentroDeZonaAsync(string zonaSlug, double latitud, double longitud, CancellationToken cancellationToken)
        {
            if (await _zonas.ContienePuntoAsync(zonaSlug, latitud, longitud, cancellationToken))
                return;

            if (!await _zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NoEncontradoException("No se encontro una zona con ese identificador.");

            throw new DatosInvalidosException("El punto tiene que estar dentro de la zona.");
        }

        private static Point Punto(double latitud, double longitud) => new(longitud, latitud) { SRID = 4326 };

        private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        private static AporteDto ADto(Aporte aporte, string? emailAutor, Guid? usuarioActual) =>
            new(
                aporte.Id,
                aporte.Tipo,
                aporte.Estado,
                Autor.Iniciales(emailAutor),
                aporte.FechaCreacion,
                aporte.UsuarioId == usuarioActual,
                aporte.Validaciones.FirstOrDefault(v => v.UsuarioId == usuarioActual)?.Confirma,
                aporte.Confirmaciones,
                aporte.Rechazos,
                aporte.Nombre,
                aporte.Categoria,
                aporte.Ubicacion is null ? null : new CoordenadaDto(aporte.Ubicacion.Y, aporte.Ubicacion.X),
                aporte.Horario,
                aporte.Direccion,
                aporte.ViveOTrabajaEnZona,
                aporte.PuntoInteresId,
                aporte.PuntoInteresNombre,
                aporte.Motivo,
                aporte.Comentario);
    }
}
