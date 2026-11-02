using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // A user's profile: their Context of type profile (one per user).
    public interface IProfileRepository
    {
        // Includes details and change tracking; null when the profile has not been created.
        Task<Context?> GetByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Perfiles existentes de varios usuarios, con sus preferencias y restricciones.
        Task<IReadOnlyList<Context>> GetByUserIdsAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default);

        void Add(Context perfil);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
