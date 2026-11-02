using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IContributionRepository
    {
        Task<(int Aprobados, int Pendientes)> CountAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Returns pending contributions for a zone with their authors and votes, oldest first (read-only).
        Task<IReadOnlyList<Contribution>> ListPendingAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Returns the contribution with its votes, tracked for changes.
        Task<Contribution?> GetAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Contribution aporte);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
