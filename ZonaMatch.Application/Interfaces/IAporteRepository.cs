using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAporteRepository
    {
        Task<(int Aprobados, int Pendientes)> ContarAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Pending contributions of the zone with their author and votes, oldest first (read only)
        Task<IReadOnlyList<Aporte>> ListarPendientesAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Tracked, with its votes
        Task<Aporte?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Aporte aporte);

        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
