using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Sugiere una correccion de un lugar del mapa: cerro, o su nombre, ubicacion o categoria estan mal
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
            string zonaSlug, SesionDto usuario, CrearCorreccionDto solicitud, CancellationToken cancellationToken = default)
        {
            var motivo = solicitud.Motivo!.Value; // garantizado por [Required] en el DTO

            var aporte = new Aporte
            {
                ZonaSlug = zonaSlug,
                UsuarioId = usuario.Id,
                Tipo = TipoAporte.Correccion,
                PuntoInteresId = solicitud.PuntoInteresId,
                PuntoInteresNombre = solicitud.PuntoInteresNombre.Trim(),
                Motivo = motivo,
                Comentario = AporteMapeo.Limpiar(solicitud.Comentario),
                FechaCreacion = _reloj.GetUtcNow()
            };

            // Solo se guarda el valor que corresponde al motivo
            switch (motivo)
            {
                case MotivoCorreccion.Nombre:
                    aporte.Nombre = AporteMapeo.Limpiar(solicitud.NombrePropuesto)
                        ?? throw new DatosInvalidosException("Indica el nombre correcto del lugar.");
                    break;

                case MotivoCorreccion.Categoria:
                    aporte.Categoria = AporteMapeo.ValidarCategoria(solicitud.CategoriaPropuesta);
                    break;

                case MotivoCorreccion.Ubicacion:
                    if (solicitud.Latitud is not { } latitud || solicitud.Longitud is not { } longitud)
                        throw new DatosInvalidosException("Marca en el mapa la ubicacion correcta del lugar.");
                    await _zonas.ValidarContienePuntoAsync(zonaSlug, latitud, longitud, cancellationToken);
                    aporte.Ubicacion = AporteMapeo.Punto(latitud, longitud);
                    break;

                case MotivoCorreccion.Otro when aporte.Comentario is null:
                    throw new DatosInvalidosException("Contanos que hay que corregir en el comentario.");
            }

            // La validacion de ubicacion de arriba ya cubre la zona; los otros motivos la validan aca
            if (motivo != MotivoCorreccion.Ubicacion)
                await _zonas.ValidarExisteAsync(zonaSlug, cancellationToken);

            _aportes.Agregar(aporte);
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return AporteMapeo.ADto(aporte, usuario.Email, usuario.Id);
        }
    }
}
