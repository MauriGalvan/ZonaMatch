using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAnalisisRepository
    {
        Task<IReadOnlyList<Contexto>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Returns null when there is no such analysis for that user.
        Task<Contexto?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        Task AddAsync(Contexto contexto, CancellationToken cancellationToken = default);

        Task UpdateAsync(Contexto contexto, CancellationToken cancellationToken = default);

        Task DeleteAsync(Contexto contexto, CancellationToken cancellationToken = default);
    }
}
