using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IResenaRepository
    {
        // Zone reviews with their authors and "useful" votes, newest first (read-only).
        Task<IReadOnlyList<Resena>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Tracked entities are persisted by the repository.
        Task<Resena?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default);

        // Tracked entities with their "useful" votes.
        Task<Resena?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Resena resena);
        void Eliminar(Resena resena);

        // Throws ConflictException when a concurrent request already created the user's review.
        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
