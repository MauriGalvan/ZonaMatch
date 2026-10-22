using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Suggests a place missing from the map; it must be inside the zone.
    public class CrearPuntoNuevo
    {
        private readonly IAporteRepository _aportes;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _reloj;

        public CrearPuntoNuevo(IAporteRepository aportes, IZonaRepository zonas, TimeProvider reloj)
        {
            _aportes = aportes;
            _zonas = zonas;
            _reloj = reloj;
        }

        public async Task<AporteDto> EjecutarAsync(
            string zonaSlug, AuthenticatedUserContext currentUser, CrearPuntoNuevoDto solicitud, CancellationToken cancellationToken = default)
        {
            var categoria = AporteMapeo.ValidarCategoria(solicitud.Categoria);
            await _zonas.ValidarContienePuntoAsync(zonaSlug, solicitud.Latitud, solicitud.Longitud, cancellationToken);

            var aporte = new Aporte
            {
                ZonaSlug = zonaSlug,
                UsuarioId = currentUser.Id,
                Tipo = TipoAporte.PuntoNuevo,
                Categoria = categoria,
                Nombre = solicitud.Nombre.Trim(),
                Ubicacion = AporteMapeo.Punto(solicitud.Latitud, solicitud.Longitud),
                Horario = AporteMapeo.Limpiar(solicitud.Horario),
                Direccion = AporteMapeo.Limpiar(solicitud.Direccion),
                ViveOTrabajaEnZona = solicitud.ViveOTrabajaEnZona,
                FechaCreacion = _reloj.GetUtcNow()
            };

            _aportes.Agregar(aporte);
            await _aportes.GuardarCambiosAsync(cancellationToken);

            return AporteMapeo.ADto(aporte, currentUser.Email, currentUser.Id);
        }
    }
}
