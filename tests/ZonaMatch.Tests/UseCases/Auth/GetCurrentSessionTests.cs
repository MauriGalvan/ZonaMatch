using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Auth;

public sealed class GetCurrentSessionTests
{
    [Theory]
    [InlineData("María José", "De la Peña")]
    [InlineData(null, null)]
    public async Task ExecuteAsync_WhenUserExists_ReturnsPersistedNames(string? firstName, string? lastName)
    {
        var user = new User { Email = "user@example.com", FirstName = firstName, LastName = lastName };
        var session = new AuthenticatedUserContext(user.Id, user.Email);
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetByIdAsync(user.Id, CancellationToken.None)).ReturnsAsync(user);
        var useCase = new GetCurrentSession(users.Object);

        var response = await useCase.ExecuteAsync(session, isAdmin: true);

        response.Id.Should().Be(user.Id);
        response.Email.Should().Be(user.Email);
        response.IsAdmin.Should().BeTrue();
        response.FirstName.Should().Be(firstName);
        response.LastName.Should().Be(lastName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_RejectsSession()
    {
        var session = new AuthenticatedUserContext(Guid.NewGuid(), "user@example.com");
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetByIdAsync(session.Id, CancellationToken.None)).ReturnsAsync((User?)null);
        var useCase = new GetCurrentSession(users.Object);

        var act = () => useCase.ExecuteAsync(session, isAdmin: false);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
