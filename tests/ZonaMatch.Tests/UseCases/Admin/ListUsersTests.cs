using ZonaMatch.Application.DTOs.Admin.Responses;
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
        var useCase = new ListUsers(new FakeAdminUserRepository(administrator, member));

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
        var useCase = new ListUsers(new FakeAdminUserRepository());

        Assert.Empty(await useCase.ExecuteAsync());
    }
}
