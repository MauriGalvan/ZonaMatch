using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Services
{
    public class ZonaService : IZonaService
    {
        // ~15 m: the boundary keeps the shape of the barrio and stays light
        public const double ToleranciaPorDefecto = 0.00015;

        public const int MinCaracteresBusqueda = 2;
        public const int MaxResultadosBusqueda = 20;

        private readonly IZonaRepository _zonaRepository;
        private readonly IPuntoInteresRepository _puntoInteresRepository;

        public ZonaService(IZonaRepository zonaRepository, IPuntoInteresRepository puntoInteresRepository)
        {
            _zonaRepository = zonaRepository;
            _puntoInteresRepository = puntoInteresRepository;
        }

        public Task<string?> GetPorSlugAsync(string slug, double? tolerancia, CancellationToken cancellationToken = default) =>
            _zonaRepository.GetPorSlugAsync(slug, Math.Max(tolerancia ?? ToleranciaPorDefecto, 0), cancellationToken);

        public async Task<IReadOnlyList<ZonaResumenDto>> BuscarAsync(
            string texto, int limite, CancellationToken cancellationToken = default)
        {
            // Searching by slug ignores case, accents and punctuation the same way the zone URLs do
            var fragmento = SlugZona.Generar(texto);
            if (fragmento.Length < MinCaracteresBusqueda)
                return [];

            return await _zonaRepository.BuscarAsync(
                fragmento, Math.Clamp(limite, 1, MaxResultadosBusqueda), cancellationToken);
        }

        public async Task<PuntosInteresCercanosDto> GetPuntosInteresAsync(
            string slug, IReadOnlyCollection<string> categorias, CancellationToken cancellationToken = default)
        {
            // Same limit as the radius search: big zones (a partido) return the points closest to the center
            var puntos = await _puntoInteresRepository.GetEnZonaAsync(
                slug, categorias, GeolocationService.MaxPuntosInteres + 1, cancellationToken);

            return GeolocationService.Recortar(puntos);
        }

        public async Task<ResumenPuntosInteresDto> GetResumenPuntosInteresAsync(
            string slug, CancellationToken cancellationToken = default)
        {
            var cantidades = await _puntoInteresRepository.ContarPorTipoEnZonaAsync(slug, cancellationToken);
            return new ResumenPuntosInteresDto(RadioMetros: null, GeolocationService.ResumirPorCategoria(cantidades));
        }
    }
}
