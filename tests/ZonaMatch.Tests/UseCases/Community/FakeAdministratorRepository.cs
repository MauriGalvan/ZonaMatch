using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Community;

// Only the administrators lookup is used by the community use cases.
internal sealed class FakeAdministratorRepository : IUserRepository
{
    public List<string> AdministratorEmails { get; } = [];
    public List<Guid> AdministratorIds { get; } = [];
    public bool FailLookup { get; set; }

    public Task<IReadOnlyList<string>> GetAdministratorEmailsAsync(CancellationToken cancellationToken = default) =>
        FailLookup
            ? Task.FromException<IReadOnlyList<string>>(new InvalidOperationException("The database is not available."))
            : Task.FromResult<IReadOnlyList<string>>(AdministratorEmails.ToList());

    public Task<IReadOnlyList<Guid>> GetAdministratorIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(AdministratorIds.ToList());

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>([]);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<User?>(null);

    public Task SetAdministratorAsync(Guid id, bool isAdministrator, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
