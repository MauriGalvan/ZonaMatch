using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class GeolocationService : IGeolocationService
    {
        private readonly IEscuelaRepository _escuelaRepository;
        private readonly IEspacioVerdeRepository _espacioVerdeRepository;
        private readonly IBarrioRepository _barrioRepository;
        private readonly IZonaRepository _zonaRepository;

        public GeolocationService(
            IEscuelaRepository escuelaRepository,
            IEspacioVerdeRepository espacioVerdeRepository,
            IBarrioRepository barrioRepository,
            IZonaRepository zonaRepository)
        {
            _escuelaRepository = escuelaRepository;
            _espacioVerdeRepository = espacioVerdeRepository;
            _barrioRepository = barrioRepository;
            _zonaRepository = zonaRepository;
        }

        public async Task<UbicacionResumenDto> GetResumenUbicacionAsync(double latitud, double longitud, double radioMetros)
        {
            var barrioTask = _barrioRepository.GetQueContienePuntoAsync(latitud, longitud);
            var zonaTask = _zonaRepository.GetQueContienePuntoAsync(latitud, longitud);
            var escuelasTask = GetEscuelasCercanasAsync(latitud, longitud, radioMetros);
            var espaciosTask = GetEspaciosVerdesCercanosAsync(latitud, longitud, radioMetros);

            await Task.WhenAll(barrioTask, zonaTask, escuelasTask, espaciosTask);

            return new UbicacionResumenDto(
                new CoordenadaDto(latitud, longitud),
                MapBarrio(barrioTask.Result),
                MapZona(zonaTask.Result),
                escuelasTask.Result,
                espaciosTask.Result);
        }

        public async Task<IReadOnlyList<EscuelaDto>> GetEscuelasCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel = null, string? gestion = null)
        {
            var escuelas = await _escuelaRepository.GetCercanasAsync(latitud, longitud, radioMetros, nivel, gestion);
            return escuelas.Select(r => MapEscuela(r.Entidad, r.DistanciaMetros)).ToList();
        }

        public async Task<IReadOnlyList<EspacioVerdeDto>> GetEspaciosVerdesCercanosAsync(double latitud, double longitud, double radioMetros)
        {
            var espacios = await _espacioVerdeRepository.GetCercanosAsync(latitud, longitud, radioMetros);
            return espacios.Select(r => MapEspacioVerde(r.Entidad, r.DistanciaMetros)).ToList();
        }

        public async Task<BarrioDto?> GetBarrioPorUbicacionAsync(double latitud, double longitud)
        {
            var barrio = await _barrioRepository.GetQueContienePuntoAsync(latitud, longitud);
            return MapBarrio(barrio);
        }

        public async Task<ZonaDto?> GetZonaPorUbicacionAsync(double latitud, double longitud)
        {
            var zona = await _zonaRepository.GetQueContienePuntoAsync(latitud, longitud);
            return MapZona(zona);
        }

        private static EscuelaDto MapEscuela(Escuela e, double distanciaMetros) =>
            new(e.Id, e.Nombre, e.Nivel, e.Gestion, e.Direccion, e.Ubicacion.Y, e.Ubicacion.X, distanciaMetros);

        private static EspacioVerdeDto MapEspacioVerde(EspacioVerde e, double distanciaMetros) =>
            new(e.Id, e.Nombre, e.Tipo, e.SuperficieM2, e.Ubicacion.Y, e.Ubicacion.X, distanciaMetros);

        private static BarrioDto? MapBarrio(Barrio? b) =>
            b is null ? null : new BarrioDto(b.Id, b.Nombre, b.Comuna, b.Poblacion);

        private static ZonaDto? MapZona(Zona? z) =>
            z is null ? null : new ZonaDto(z.Id, z.Nombre, z.Descripcion);
    }
}
