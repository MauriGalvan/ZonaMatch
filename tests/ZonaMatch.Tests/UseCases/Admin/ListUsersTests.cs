using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Admin;

public sealed class ListUsersTests
{
    [Fact]
    public async Task ListUsers_ReturnsEveryUserWithTheirAdminFlag()
    {
        var administrator = new User { Email = "admin@example.com", DateOfBirth = new DateOnly(1990, 4, 12), IsAdministrator = true };
        var member = new User { Email = "ana@example.com", DateOfBirth = new DateOnly(1995, 8, 23) };
        var useCase = new ListUsers(new FakeUserRepository([administrator, member]));

        var users = await useCase.ExecuteAsync();

        Assert.Equal(
            [
                new AdminUserResponse(administrator.Id, "admin@example.com", new DateOnly(1990, 4, 12), IsAdmin: true),
                new AdminUserResponse(member.Id, "ana@example.com", new DateOnly(1995, 8, 23), IsAdmin: false),
            ],
            users);
    }

    [Fact]
    public async Task ListUsers_WhenNoUsers_ReturnsEmptyList()
    {
        var useCase = new ListUsers(new FakeUserRepository([]));

        Assert.Empty(await useCase.ExecuteAsync());
    }

    private sealed class FakeUserRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(users);

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
