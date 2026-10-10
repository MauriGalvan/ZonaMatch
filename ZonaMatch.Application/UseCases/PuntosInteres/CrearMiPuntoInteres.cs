using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.PuntosInteres
{
    // Alta de un punto de interes personal elegido en el mapa. Direccion y barrio salen de las coordenadas
    // (el mismo geocodificador de GET /Geolocation/direccion), asi el cliente no puede guardar datos inventados.
    // El punto tiene que caer en un partido o comuna: la app solo cubre el AMBA.
    public class CrearMiPuntoInteres
    {
        private readonly IPuntoInteresRepository _puntos;
        private readonly IGeolocationService _geolocalizacion;
        private readonly TimeProvider _reloj;

        public CrearMiPuntoInteres(IPuntoInteresRepository puntos, IGeolocationService geolocalizacion, TimeProvider reloj)
        {
            _puntos = puntos;
            _geolocalizacion = geolocalizacion;
            _reloj = reloj;
        }

        public async Task<PuntoInteresUsuarioDto> EjecutarAsync(
            Guid usuarioId, CrearPuntoInteresUsuarioDto solicitud, CancellationToken cancellationToken = default)
        {
            var tag = TagPuntoInteres.LimpiarNombre(solicitud.Tag);
            if (tag.Length < Longitudes.TagPuntoUsuarioMinimo)
                throw new DatosInvalidosException($"El tag debe tener al menos {Longitudes.TagPuntoUsuarioMinimo} caracteres.");

            var direccion = await _geolocalizacion.GetDireccionPorUbicacionAsync(solicitud.Latitud, solicitud.Longitud, cancellationToken);
            if (direccion is null || (direccion.Partido is null && direccion.Comuna is null))
                throw new DatosInvalidosException("El punto esta fuera del area cubierta (AMBA).");

            var punto = new PuntoInteresUsuario
            {
                UsuarioId = usuarioId,
                Alias = AporteMapeo.Limpiar(solicitud.Alias),
                Ubicacion = AporteMapeo.Punto(solicitud.Latitud, solicitud.Longitud),
                Direccion = Recortar(TextoDireccion(direccion), Longitudes.Direccion),
                Barrio = Recortar(direccion.Localidad, Longitudes.NombreLugar),
                FechaCreacion = _reloj.GetUtcNow()
            };

            // Crea el tag si no existe y guarda tag y punto juntos
            await _puntos.AgregarDeUsuarioAsync(punto, tag, cancellationToken);

            return PuntoInteresUsuarioMapeo.ADto(punto);
        }

        // "Somellera 1499"; si el geocodificador no encontro calle, el nombre del lugar o la direccion completa
        private static string? TextoDireccion(DireccionDto direccion)
        {
            var calleYAltura = string.Join(' ', new[] { direccion.Calle, direccion.Altura }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return calleYAltura.Length > 0 ? calleYAltura : direccion.Nombre ?? direccion.DireccionCompleta;
        }

        private static string? Recortar(string? texto, int maximo)
        {
            var limpio = AporteMapeo.Limpiar(texto);
            return limpio is null || limpio.Length <= maximo ? limpio : limpio[..maximo];
        }
    }
}
