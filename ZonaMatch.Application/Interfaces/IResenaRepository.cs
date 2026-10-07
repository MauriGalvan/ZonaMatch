using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IResenaRepository
    {
        // Reviews of the zone with their author and "useful" votes, newest first (read only)
        Task<IReadOnlyList<Resena>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Tracked: changes are saved with GuardarCambiosAsync
        Task<Resena?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default);

        // Tracked, with its "useful" votes
        Task<Resena?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Resena resena);
        void Eliminar(Resena resena);

        // Throws OperacionNoPermitidaException if a concurrent request already created the user's review
        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
