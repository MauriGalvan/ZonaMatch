using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    // Emails of the site administrators (User.IsAdministrator).
    Task<IReadOnlyList<string>> GetAdministratorEmailsAsync(CancellationToken cancellationToken = default);

    // Ids of the site administrators, to notify them in the app.
    Task<IReadOnlyList<Guid>> GetAdministratorIdsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    // Every registered user, ordered by email.
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    // Read-only.
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetAdministratorAsync(Guid id, bool isAdministrator, CancellationToken cancellationToken = default);

    // Investor profile access (User.InvestorAccess) and when it was requested and granted.
    Task SetInvestorAccessAsync(
        Guid id, string? access, DateTimeOffset? requestedAt, DateTimeOffset? grantedAt, CancellationToken cancellationToken = default);
}
