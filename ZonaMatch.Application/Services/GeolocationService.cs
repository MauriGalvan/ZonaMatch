using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class GeolocationService : IGeolocationService
    {
        // Keeps the payload and the markers drawn on the map reasonable
        public const int MaxPuntosInteres = 1_500;
        public const double MaxRadioPuntosInteresMetros = 5_000;

        private readonly IEscuelaRepository _escuelaRepository;
        private readonly IPartidoRepository _partidoRepository;
        private readonly IComunaRepository _comunaRepository;
        private readonly IGeocodingClient _geocodingClient;
        private readonly IPuntoInteresRepository _puntoInteresRepository;

        public GeolocationService(
            IEscuelaRepository escuelaRepository,
            IPartidoRepository partidoRepository,
            IComunaRepository comunaRepository,
            IGeocodingClient geocodingClient,
            IPuntoInteresRepository puntoInteresRepository)
        {
            _escuelaRepository = escuelaRepository;
            _partidoRepository = partidoRepository;
            _comunaRepository = comunaRepository;
            _geocodingClient = geocodingClient;
            _puntoInteresRepository = puntoInteresRepository;
        }

        public async Task<PuntosInteresCercanosDto> GetPuntosInteresCercanosAsync(
            double latitud, double longitud, double radioMetros, IReadOnlyCollection<string> categorias,
            CancellationToken cancellationToken = default)
        {
            // One extra row tells whether the result was cut at the limit
            var puntos = await _puntoInteresRepository.GetCercanosAsync(
                latitud, longitud, radioMetros, categorias, MaxPuntosInteres + 1, cancellationToken);

            return Recortar(puntos);
        }

        public async Task<ResumenPuntosInteresDto> GetResumenPuntosInteresAsync(
            double latitud, double longitud, double radioMetros, CancellationToken cancellationToken = default)
        {
            var cantidades = await _puntoInteresRepository.ContarPorTipoAsync(latitud, longitud, radioMetros, cancellationToken);
            return new ResumenPuntosInteresDto(radioMetros, ResumirPorCategoria(cantidades));
        }

        // Points come with one extra row (MaxPuntosInteres + 1): if it is there, the result was cut at the limit
        internal static PuntosInteresCercanosDto Recortar(IReadOnlyList<PuntoInteresDto> puntos) =>
            puntos.Count > MaxPuntosInteres
                ? new PuntosInteresCercanosDto(puntos.Take(MaxPuntosInteres).ToList(), Truncado: true)
                : new PuntosInteresCercanosDto(puntos, Truncado: false);

        // Every category, with its types ordered by count
        internal static IReadOnlyList<ResumenCategoriaDto> ResumirPorCategoria(IReadOnlyList<CantidadPorTipo> cantidades) =>
            CategoriaPuntoInteres.Todas
                .Select(categoria =>
                {
                    var tipos = cantidades
                        .Where(c => c.Categoria == categoria)
                        .OrderByDescending(c => c.Cantidad)
                        .Select(c => new CantidadPorTipoDto(c.Tipo, c.Cantidad))
                        .ToList();

                    return new ResumenCategoriaDto(categoria, tipos.Sum(t => t.Cantidad), tipos);
                })
                .ToList();

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
