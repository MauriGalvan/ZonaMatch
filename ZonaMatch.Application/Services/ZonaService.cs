using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.Services
{
    public class ZonaService : IZonaService
    {
        // ~15 m: the boundary keeps the shape of the barrio and stays light
        public const double ToleranciaPorDefecto = 0.00015;

        private readonly IZonaRepository _zonaRepository;

        public ZonaService(IZonaRepository zonaRepository)
        {
            _zonaRepository = zonaRepository;
        }

        public Task<string?> GetPorSlugAsync(string slug, double? tolerancia, CancellationToken cancellationToken = default) =>
            _zonaRepository.GetPorSlugAsync(slug, Math.Max(tolerancia ?? ToleranciaPorDefecto, 0), cancellationToken);
    }
}
