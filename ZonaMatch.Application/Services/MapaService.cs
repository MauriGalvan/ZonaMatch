using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.Services
{
    public class MapaService : IMapaService
    {
        // Upper bound of features per viewport request, to keep payloads and map rendering reasonable
        public const int MaxFeaturesPorConsulta = 5_000;

        // ~55 m: invisible at city/province zoom, shrinks the polygons a lot
        private const double ToleranciaPorDefecto = 0.0005;
        private const double ToleranciaMaxima = 0.05;

        // Assumed map width in pixels to derive a viewport-based tolerance (about 1 px per vertex)
        private const double AnchoMapaPx = 1500;

        private readonly IMapaRepository _mapaRepository;

        public MapaService(IMapaRepository mapaRepository)
        {
            _mapaRepository = mapaRepository;
        }

        public Task<string> GetPartidosAsync(double? tolerancia) =>
            _mapaRepository.GetPartidosAsync(Normalizar(tolerancia, ToleranciaPorDefecto));

        public Task<string> GetComunasAsync(double? tolerancia) =>
            _mapaRepository.GetComunasAsync(Normalizar(tolerancia, ToleranciaPorDefecto));

        public async Task<string?> GetRadiosAsync(BoundingBox bbox, double? tolerancia)
        {
            if (await _mapaRepository.ContarRadiosAsync(bbox) > MaxFeaturesPorConsulta)
                return null;

            var porDefecto = (bbox.MaxLon - bbox.MinLon) / AnchoMapaPx;
            return await _mapaRepository.GetRadiosAsync(bbox, Normalizar(tolerancia, porDefecto));
        }

        public async Task<string?> GetEscuelasAsync(BoundingBox bbox, string? nivel, string? sector)
        {
            if (await _mapaRepository.ContarEscuelasAsync(bbox, nivel, sector) > MaxFeaturesPorConsulta)
                return null;

            return await _mapaRepository.GetEscuelasAsync(bbox, nivel, sector);
        }

        private static double Normalizar(double? tolerancia, double porDefecto) =>
            Math.Clamp(tolerancia ?? porDefecto, 0d, ToleranciaMaxima);
    }
}
