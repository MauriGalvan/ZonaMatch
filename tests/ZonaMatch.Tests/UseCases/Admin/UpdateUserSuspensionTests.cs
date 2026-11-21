using FluentAssertions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Admin;

public class UpdateUserSuspensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);

    private readonly User _administrator = new() { Email = "admin@example.com", IsAdministrator = true };
    private readonly User _otherAdministrator = new() { Email = "admin2@example.com", IsAdministrator = true };
    private readonly User _member = new() { Email = "ana@example.com" };
    private readonly FakeAdminUserRepository _users;
    private readonly UpdateUserSuspension _useCase;

    public UpdateUserSuspensionTests()
    {
        _users = new FakeAdminUserRepository(_administrator, _otherAdministrator, _member);
        _useCase = new UpdateUserSuspension(_users, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task UpdateSuspension_WhenSuspending_StoresTheDateAndReturnsTheUserSuspended()
    {
        var response = await _useCase.ExecuteAsync(_member.Id, suspended: true, _administrator.Id);

        _member.SuspendedAt.Should().Be(Now);
        response.IsSuspended.Should().BeTrue();
        _users.SetSuspendedCalls.Should().Be(1);
    }

    [Fact]
    public async Task UpdateSuspension_WhenReactivating_ClearsTheDate()
    {
        _member.SuspendedAt = Now.AddDays(-1);

        var response = await _useCase.ExecuteAsync(_member.Id, suspended: false, _administrator.Id);

        _member.SuspendedAt.Should().BeNull();
        response.IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSuspension_WhenAlreadySuspended_KeepsTheFirstDateWithoutWriting()
    {
        var suspendedAt = Now.AddDays(-3);
        _member.SuspendedAt = suspendedAt;

        await _useCase.ExecuteAsync(_member.Id, suspended: true, _administrator.Id);

        _member.SuspendedAt.Should().Be(suspendedAt);
        _users.SetSuspendedCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSuspension_WhenSuspendingAnAdministrator_ThrowsBadRequest()
    {
        var act = () => _useCase.ExecuteAsync(_otherAdministrator.Id, suspended: true, _administrator.Id);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("No se puede suspender a un administrador*");
        _otherAdministrator.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSuspension_WhenSuspendingOwnAccount_ThrowsBadRequest()
    {
        var act = () => _useCase.ExecuteAsync(_administrator.Id, suspended: true, _administrator.Id);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("No podés suspender tu propia cuenta.");
        _users.SetSuspendedCalls.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSuspension_WhenUserDoesNotExist_ThrowsNotFound()
    {
        var act = () => _useCase.ExecuteAsync(Guid.NewGuid(), suspended: true, _administrator.Id);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("El usuario no existe.");
    }
}
