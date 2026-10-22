using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Suggests a map correction when a place closed or its name, location, or category is incorrect.
    public class CrearCorreccion
    {
        private readonly IAporteRepository _aportes;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _reloj;

        public CrearCorreccion(IAporteRepository aportes, IZonaRepository zonas, TimeProvider reloj)
        {
            _aportes = aportes;
            _zonas = zonas;
            _reloj = reloj;
        }

        public async Task<AporteDto> EjecutarAsync(
            string zonaSlug, AuthenticatedUserContext currentUser, CrearCorreccionDto solicitud, CancellationToken cancellationToken = default)
        {
            var motivo = solicitud.Motivo!.Value; // Required by DTO validation.

            var aporte = new Aporte
            {
                ZonaSlug = zonaSlug,
                UsuarioId = currentUser.Id,
                Tipo = TipoAporte.Correccion,
                PuntoInteresId = solicitud.PuntoInteresId,
                PuntoInteresNombre = solicitud.PuntoInteresNombre.Trim(),
                Motivo = motivo,
                Comentario = AporteMapeo.Limpiar(solicitud.Comentario),
                FechaCreacion = _reloj.GetUtcNow()
            };

            // Store only the value associated with the selected reason.
            switch (motivo)
            {
                case MotivoCorreccion.Nombre:
                    aporte.Nombre = AporteMapeo.Limpiar(solicitud.NombrePropuesto)
                        ?? throw new BadRequestException("Indica el nombre correcto del lugar.");
                    break;

                case MotivoCorreccion.Categoria:
                    aporte.Categoria = AporteMapeo.ValidarCategoria(solicitud.CategoriaPropuesta);
                    break;

                case MotivoCorreccion.Ubicacion:
                    if (solicitud.Latitud is not { } latitud || solicitud.Longitud is not { } longitud)
                        throw new BadRequestException("Marca en el mapa la ubicacion correcta del lugar.");
                    await _zonas.ValidarContienePuntoAsync(zonaSlug, latitud, longitud, cancellationToken);
                    aporte.Ubicacion = AporteMapeo.Punto(latitud, longitud);
                    break;

                case MotivoCorreccion.Otro when aporte.Comentario is null:
                    throw new BadRequestException("Contanos que hay que corregir en el comentario.");
            }

            // Location validation above already checks the zone; other reasons are validated here.
            if (motivo != MotivoCorreccion.Ubicacion)
                await _zonas.ValidarExisteAsync(zonaSlug, cancellationToken);

            _aportes.Agregar(aporte);
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return AporteMapeo.ADto(aporte, currentUser.Email, currentUser.Id);
        }
    }
}
