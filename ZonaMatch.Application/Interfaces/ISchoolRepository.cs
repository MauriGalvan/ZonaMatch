using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ISchoolRepository : IPointEntityRepository<School>
    {
        // Same as GetCercanosAsync, with optional filters by nivel and sector
        Task<IReadOnlyList<(School Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? sector);
    }
}
