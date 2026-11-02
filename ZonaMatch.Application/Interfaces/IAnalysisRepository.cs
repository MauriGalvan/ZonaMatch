using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Saved analyses are loaded with their criteria and zones and tracked for changes.
    // Call SaveChangesAsync to persist updates.
    public interface IAnalysisRepository
    {
        // Returns the user's original analyses and versions, newest first.
        Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Returns null when the analysis does not exist or belongs to another user.
        Task<Analysis?> GetAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        // Returns versions derived from an original, oldest first.
        Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid analisisOrigenId, CancellationToken cancellationToken = default);

        void Add(Analysis analisis);

        // Deletes the analysis together with its criteria snapshot.
        void Delete(Analysis analisis);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
