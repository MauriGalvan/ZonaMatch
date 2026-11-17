using FluentAssertions;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Admin;

public class ListUsersTests
{
    [Fact]
    public async Task ListUsers_ReturnsEveryUserWithTheirAdminFlag()
    {
        var administrator = new User { Email = "admin@example.com", DateOfBirth = new DateOnly(1990, 4, 12), IsAdministrator = true };
        var member = new User { Email = "ana@example.com", DateOfBirth = new DateOnly(1995, 8, 23) };
        var useCase = new ListUsers(new FakeAdminUserRepository(administrator, member));

        var users = await useCase.ExecuteAsync();

        users.Should().Equal(
            new AdminUserResponse(administrator.Id, "admin@example.com", new DateOnly(1990, 4, 12), IsAdmin: true, RegisteredAt: administrator.CreatedAt),
            new AdminUserResponse(member.Id, "ana@example.com", new DateOnly(1995, 8, 23), IsAdmin: false, RegisteredAt: member.CreatedAt));
    }

    [Fact]
    public async Task ListUsers_WhenNoUsers_ReturnsEmptyList()
    {
        var useCase = new ListUsers(new FakeAdminUserRepository());

        var users = await useCase.ExecuteAsync();

        users.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUsers_WhenRepositoryReturnsUsers_KeepsTheRepositoryOrder()
    {
        var useCase = new ListUsers(new FakeAdminUserRepository(
            new User { Email = "carla@example.com" },
            new User { Email = "ana@example.com" },
            new User { Email = "bruno@example.com" }));

        var users = await useCase.ExecuteAsync();

        users.Select(user => user.Email).Should().Equal("carla@example.com", "ana@example.com", "bruno@example.com");
    }

    [Fact]
    public async Task ListUsers_WhenSeveralAdministrators_FlagsEachOneAsAdmin()
    {
        var useCase = new ListUsers(new FakeAdminUserRepository(
            new User { Email = "admin1@example.com", IsAdministrator = true },
            new User { Email = "ana@example.com" },
            new User { Email = "admin2@example.com", IsAdministrator = true }));

        var users = await useCase.ExecuteAsync();

        users.Where(user => user.IsAdmin).Select(user => user.Email)
            .Should().BeEquivalentTo("admin1@example.com", "admin2@example.com");
    }

    [Fact]
    public async Task ListUsers_ReturnsTheRegistrationDateOfEachUser()
    {
        var registeredAt = new DateTimeOffset(2026, 11, 13, 20, 30, 0, TimeSpan.Zero);
        var useCase = new ListUsers(new FakeAdminUserRepository(new User { Email = "ana@example.com", CreatedAt = registeredAt }));

        var users = await useCase.ExecuteAsync();

        users.Should().ContainSingle().Which.RegisteredAt.Should().Be(registeredAt);
    }
}
