using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Admin;

// In-memory user store for the admin use cases.
internal sealed class FakeAdminUserRepository(params User[] users) : IUserRepository
{
    public List<User> Users { get; } = [.. users];

    public int SetAdministratorCalls { get; private set; }

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users.OrderBy(user => user.Email).ToList());

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

    public Task SetAdministratorAsync(Guid id, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        SetAdministratorCalls++;
        Users.Single(user => user.Id == id).IsAdministrator = isAdministrator;
        return Task.CompletedTask;
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
