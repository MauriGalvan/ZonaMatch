using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IUsuarioRepository
    {
        // The email must already be normalized (trimmed, lowercase)
        Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken = default);

        // The email must already be normalized. Returns null when there is no such user.
        Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default);

        // Throws EmailYaRegistradoException if the unique index on email is violated (concurrent registrations)
        Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);
    }
}
