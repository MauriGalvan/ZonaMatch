using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Interfaces
{
    // Datos de todas las zonas para el motor de puntaje (censo, delitos, transporte, equipamiento y alquiler)
    public interface IZoneProfilesRepository
    {
        // Todas las zonas con datos; el motor compara cada una con las demas de su tipo
        Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default);

        // Zones containing geocoded locations, contiguous zones at the same level, and zones crossed by direct
        // segments between each pair of locations (using the more detailed level of the pair).
        Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
            IReadOnlyCollection<(double Latitud, double Longitud)> ubicaciones,
            CancellationToken cancellationToken = default);
    }
}
