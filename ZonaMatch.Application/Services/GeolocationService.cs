using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class GeolocationService : IGeolocationService
    {
        private readonly IEscuelaRepository _escuelaRepository;
        private readonly IPartidoRepository _partidoRepository;
        private readonly IComunaRepository _comunaRepository;
        private readonly IGeocodingClient _geocodingClient;

        public GeolocationService(
            IEscuelaRepository escuelaRepository,
            IPartidoRepository partidoRepository,
            IComunaRepository comunaRepository,
            IGeocodingClient geocodingClient)
        {
            _escuelaRepository = escuelaRepository;
            _partidoRepository = partidoRepository;
            _comunaRepository = comunaRepository;
            _geocodingClient = geocodingClient;
        }

        public async Task<UbicacionResumenDto> GetResumenUbicacionAsync(double latitud, double longitud, double radioMetros)
        {
            // Sequential on purpose: the repositories share one scoped DbContext, which does not allow concurrent queries
            var partido = await GetPartidoPorUbicacionAsync(latitud, longitud);
            var comuna = await GetComunaPorUbicacionAsync(latitud, longitud);
            var escuelas = await GetEscuelasCercanasAsync(latitud, longitud, radioMetros);

            return new UbicacionResumenDto(new CoordenadaDto(latitud, longitud), partido, comuna, escuelas);
        }

        public async Task<IReadOnlyList<EscuelaDto>> GetEscuelasCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel = null, string? sector = null)
        {
            var escuelas = await _escuelaRepository.GetCercanasAsync(latitud, longitud, radioMetros, nivel, sector);
            return escuelas.Select(r => MapEscuela(r.Entidad, r.DistanciaMetros)).ToList();
        }

        public async Task<IReadOnlyList<EscuelaDto>> GetEscuelasMasCercanasAsync(double latitud, double longitud, int cantidad)
        {
            var escuelas = await _escuelaRepository.GetMasCercanosAsync(latitud, longitud, cantidad);
            return escuelas.Select(r => MapEscuela(r.Entidad, r.DistanciaMetros)).ToList();
        }

        public async Task<PartidoDto?> GetPartidoPorUbicacionAsync(double latitud, double longitud)
        {
            var partido = await _partidoRepository.GetQueContienePuntoAsync(latitud, longitud);
            return partido is null ? null : new PartidoDto(partido.Key, partido.Nombre, partido.Provincia);
        }

        public async Task<ComunaDto?> GetComunaPorUbicacionAsync(double latitud, double longitud)
        {
            var comuna = await _comunaRepository.GetQueContienePuntoAsync(latitud, longitud);
            return comuna is null ? null : new ComunaDto(comuna.Key, comuna.Nombre);
        }

        public async Task<DireccionDto?> GetDireccionPorUbicacionAsync(
            double latitud, double longitud, CancellationToken cancellationToken = default)
        {
            var geocodificada = await _geocodingClient.ReversaAsync(latitud, longitud, cancellationToken);

            // Sequential on purpose (shared scoped DbContext)
            var partido = await GetPartidoPorUbicacionAsync(latitud, longitud);
            var comuna = await GetComunaPorUbicacionAsync(latitud, longitud);

            if (geocodificada is null && partido is null && comuna is null)
                return null;

            return new DireccionDto(
                Ubicacion: new CoordenadaDto(latitud, longitud),
                Nombre: geocodificada?.Nombre,
                Calle: geocodificada?.Calle,
                Altura: geocodificada?.Altura,
                Localidad: geocodificada?.Localidad,
                Partido: partido?.Nombre,
                Comuna: comuna?.Nombre,
                Provincia: partido?.Provincia ?? geocodificada?.Provincia,
                CodigoPostal: geocodificada?.CodigoPostal,
                DireccionCompleta: geocodificada?.DireccionCompleta);
        }

        private static EscuelaDto MapEscuela(Escuela e, double distanciaMetros) =>
            new(e.ClaveNatural, e.Nombre, e.Nivel, e.Sector, e.Direccion, e.Localidad,
                e.Ubicacion.Y, e.Ubicacion.X, distanciaMetros);
    }
}
