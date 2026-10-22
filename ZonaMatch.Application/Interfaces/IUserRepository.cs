using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(Usuario user, CancellationToken cancellationToken = default);
}
