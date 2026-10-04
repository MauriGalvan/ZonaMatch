using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IEscuelaRepository : IGeoPuntoRepository<Escuela>
    {
        // Same as GetCercanosAsync, with optional filters by nivel and sector
        Task<IReadOnlyList<(Escuela Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? sector);
    }
}
