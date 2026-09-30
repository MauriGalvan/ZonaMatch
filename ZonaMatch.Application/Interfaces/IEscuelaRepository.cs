using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IEscuelaRepository : IGeoPuntoRepository<Escuela>
    {
        // Sobrecarga con filtros opcionales por nivel educativo y tipo de gestion
        Task<IReadOnlyList<(Escuela Entidad, double DistanciaMetros)>> GetCercanasAsync(
            double latitud, double longitud, double radioMetros, string? nivel, string? gestion);
    }
}
