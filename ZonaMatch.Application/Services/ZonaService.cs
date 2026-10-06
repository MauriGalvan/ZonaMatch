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

        public ZonaService(IZonaRepository zonaRepository)
        {
            _zonaRepository = zonaRepository;
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
    }
}
