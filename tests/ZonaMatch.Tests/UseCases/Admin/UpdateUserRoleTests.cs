using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Admin;

public sealed class UpdateUserRoleTests
{
    private readonly User _administrator = new() { Email = "admin@example.com", IsAdministrator = true };
    private readonly User _member = new() { Email = "ana@example.com", DateOfBirth = new DateOnly(1995, 8, 23) };
    private readonly FakeAdminUserRepository _users;
    private readonly UpdateUserRole _useCase;

    public UpdateUserRoleTests()
    {
        _users = new FakeAdminUserRepository(_administrator, _member);
        _useCase = new UpdateUserRole(_users);
    }

    [Fact]
    public async Task UpdateRole_WhenGrantingAdmin_PersistsAndReturnsTheNewRole()
    {
        var response = await _useCase.ExecuteAsync(_member.Id, isAdmin: true, _administrator.Id);

        Assert.True(response.IsAdmin);
        Assert.Equal(_member.Email, response.Email);
        Assert.True(_member.IsAdministrator);
    }

    [Fact]
    public async Task UpdateRole_WhenRemovingAdminFromAnotherUser_PersistsTheChange()
    {
        _member.IsAdministrator = true;

        var response = await _useCase.ExecuteAsync(_member.Id, isAdmin: false, _administrator.Id);

        Assert.False(response.IsAdmin);
        Assert.False(_member.IsAdministrator);
    }

    [Fact]
    public async Task UpdateRole_WhenRoleIsUnchanged_DoesNotWrite()
    {
        await _useCase.ExecuteAsync(_member.Id, isAdmin: false, _administrator.Id);

        Assert.Equal(0, _users.SetAdministratorCalls);
    }

    [Fact]
    public async Task UpdateRole_WhenRemovingOwnAdminRole_ThrowsBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _useCase.ExecuteAsync(_administrator.Id, isAdmin: false, _administrator.Id));

        Assert.True(_administrator.IsAdministrator);
    }

    [Fact]
    public async Task UpdateRole_WhenUserDoesNotExist_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _useCase.ExecuteAsync(Guid.NewGuid(), isAdmin: true, _administrator.Id));
    }
}
