using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Auth;

public sealed class CompleteCurrentUserNameTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNamesAreValid_SavesTrimmedNamesForAuthenticatedUser()
    {
        var session = new AuthenticatedUserContext(Guid.NewGuid(), "user@example.com");
        var request = new CompleteUserNameRequest("  Ana María  ", "  O'Neill  ");
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.TryCompleteNameAsync(session.Id, "Ana María", "O'Neill", CancellationToken.None))
            .ReturnsAsync(true);
        users.Setup(repository => repository.GetByIdAsync(session.Id, CancellationToken.None))
            .ReturnsAsync(new User { Id = session.Id, FirstName = "Ana María", LastName = "O'Neill" });
        var useCase = new CompleteCurrentUserName(users.Object);

        var response = await useCase.ExecuteAsync(session, isAdmin: true, request);

        response.Id.Should().Be(session.Id);
        response.Email.Should().Be(session.Email);
        response.IsAdmin.Should().BeTrue();
        response.FirstName.Should().Be("Ana María");
        response.LastName.Should().Be("O'Neill");
        users.Verify(repository => repository.TryCompleteNameAsync(session.Id, "Ana María", "O'Neill", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameExceedsLimit_DoesNotUpdateUser()
    {
        var session = new AuthenticatedUserContext(Guid.NewGuid(), "user@example.com");
        var request = new CompleteUserNameRequest("Ana", new string('a', User.MaximumNameLength + 1));
        var users = new Mock<IUserRepository>();
        var useCase = new CompleteCurrentUserName(users.Object);

        var act = () => useCase.ExecuteAsync(session, isAdmin: false, request);

        await act.Should().ThrowAsync<BadRequestException>();
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_RejectsSession()
    {
        var session = new AuthenticatedUserContext(Guid.NewGuid(), "user@example.com");
        var request = new CompleteUserNameRequest("Ana", "Pérez");
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.TryCompleteNameAsync(session.Id, "Ana", "Pérez", CancellationToken.None))
            .ReturnsAsync(false);
        var useCase = new CompleteCurrentUserName(users.Object);

        var act = () => useCase.ExecuteAsync(session, isAdmin: false, request);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsAlreadyComplete_ThrowsConflictException()
    {
        var session = new AuthenticatedUserContext(Guid.NewGuid(), "user@example.com");
        var request = new CompleteUserNameRequest("Julia", "López");
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.TryCompleteNameAsync(session.Id, "Julia", "López", CancellationToken.None))
            .ReturnsAsync(false);
        users.Setup(repository => repository.GetByIdAsync(session.Id, CancellationToken.None))
            .ReturnsAsync(new User { Id = session.Id, FirstName = "Ana", LastName = "Pérez" });
        var useCase = new CompleteCurrentUserName(users.Object);

        var act = () => useCase.ExecuteAsync(session, isAdmin: false, request);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
