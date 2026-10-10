using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IResenaRepository
    {
        // Resenas de la zona con su autor y sus votos de "util", la mas nueva primero (solo lectura)
        Task<IReadOnlyList<Resena>> ListarPorZonaAsync(string zonaSlug, CancellationToken cancellationToken = default);

        // Con seguimiento de cambios: se guardan con GuardarCambiosAsync
        Task<Resena?> ObtenerDeUsuarioAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default);

        // Con seguimiento de cambios, con sus votos de "util"
        Task<Resena?> ObtenerAsync(string zonaSlug, Guid id, CancellationToken cancellationToken = default);

        void Agregar(Resena resena);
        void Eliminar(Resena resena);

        // Lanza OperacionNoPermitidaException si un pedido simultaneo ya creo la resena del usuario
        Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
