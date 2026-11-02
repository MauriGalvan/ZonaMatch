using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ISchoolRepository : IPointEntityRepository<School>
    {
        // Same as GetNearbyAsync, with optional filters by level and sector.
        Task<IReadOnlyList<(School Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? sector);
    }
}
