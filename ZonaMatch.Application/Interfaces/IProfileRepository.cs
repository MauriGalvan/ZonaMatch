using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Perfil del usuario: su Context de tipo perfil (uno por usuario)
    public interface IProfileRepository
    {
        // Con su detalle y seguimiento de cambios; null si todavia no lo armo
        Task<Context?> GetByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Perfiles existentes de varios usuarios, con sus preferencias y restricciones.
        Task<IReadOnlyList<Context>> GetByUserIdsAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken = default);

        void Add(Context perfil);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
