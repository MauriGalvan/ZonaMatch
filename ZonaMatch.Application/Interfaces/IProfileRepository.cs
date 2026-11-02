using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // A user's profile: their Context of type profile (one per user).
    public interface IProfileRepository
    {
        // Includes details and change tracking; null when the profile has not been created.
        Task<Context?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        // Existing profiles for multiple users, including their preferences and constraints.
        Task<IReadOnlyList<Context>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

        void Add(Context profile);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
