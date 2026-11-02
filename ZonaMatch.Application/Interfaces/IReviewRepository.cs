using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IReviewRepository
    {
        // Zone reviews with their authors and "useful" votes, newest first (read-only).
        Task<IReadOnlyList<Review>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Tracked entities are persisted by the repository.
        Task<Review?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default);

        // Tracked entities with their "useful" votes.
        Task<Review?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Review resena);
        void Eliminar(Review resena);

        // Throws ConflictException when a concurrent request already created the user's review.
        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
