using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IAnalisisRepository
    {
        Task<IReadOnlyList<AnalisisDetallado>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        // Returns null when there is no such analysis for that user.
        Task<AnalisisDetallado?> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

        Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

        Task AddAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default);

        Task UpdateAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default);

        Task DeleteAsync(AnalisisDetallado analisis, CancellationToken cancellationToken = default);
    }
}
