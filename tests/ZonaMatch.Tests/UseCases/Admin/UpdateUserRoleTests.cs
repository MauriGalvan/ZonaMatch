using FluentAssertions;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Admin;

public class UpdateUserRoleTests
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

        response.IsAdmin.Should().BeTrue();
        response.Email.Should().Be(_member.Email);
        _member.IsAdministrator.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRole_WhenGrantingAdmin_ReturnsTheUserDetails()
    {
        var response = await _useCase.ExecuteAsync(_member.Id, isAdmin: true, _administrator.Id);

        response.Should().Be(new AdminUserResponse(_member.Id, "ana@example.com", new DateOnly(1995, 8, 23), IsAdmin: true));
    }

    [Fact]
    public async Task UpdateRole_WhenRemovingAdminFromAnotherUser_PersistsTheChange()
    {
        _member.IsAdministrator = true;

        var response = await _useCase.ExecuteAsync(_member.Id, isAdmin: false, _administrator.Id);

        response.IsAdmin.Should().BeFalse();
        _member.IsAdministrator.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRole_WhenGrantingAdmin_DoesNotChangeOtherUsers()
    {
        var otherMember = new User { Email = "bruno@example.com" };
        _users.Users.Add(otherMember);

        await _useCase.ExecuteAsync(_member.Id, isAdmin: true, _administrator.Id);

        otherMember.IsAdministrator.Should().BeFalse();
        _administrator.IsAdministrator.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRole_WhenRoleIsUnchanged_DoesNotWrite()
    {
        await _useCase.ExecuteAsync(_member.Id, isAdmin: false, _administrator.Id);

        _users.SetAdministratorCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRole_WhenAdministratorKeepsOwnAdminRole_SucceedsWithoutWriting()
    {
        var response = await _useCase.ExecuteAsync(_administrator.Id, isAdmin: true, _administrator.Id);

        response.IsAdmin.Should().BeTrue();
        _users.SetAdministratorCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRole_WhenRemovingOwnAdminRole_ThrowsBadRequest()
    {
        var act = () => _useCase.ExecuteAsync(_administrator.Id, isAdmin: false, _administrator.Id);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("No podés quitarte tu propio rol de administrador.");
        _administrator.IsAdministrator.Should().BeTrue();
        _users.SetAdministratorCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRole_WhenUserDoesNotExist_ThrowsNotFound()
    {
        var act = () => _useCase.ExecuteAsync(Guid.NewGuid(), isAdmin: true, _administrator.Id);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("El usuario no existe.");
        _users.SetAdministratorCalls.Should().Be(0);
    }
}
