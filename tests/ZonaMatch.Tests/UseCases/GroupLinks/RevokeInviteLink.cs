using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLinks;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupLinks;

public class RevokeInviteLinkTests
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly RevokeInviteLink _useCase;

    public RevokeInviteLinkTests()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new RevokeInviteLink(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenGroupDoesNotExist()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserIsNotOwner()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                // El usuario es Member o Guest, no Owner
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Solo el propietario puede revocar el enlace de invitación.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateNewTokenAndUpdateGroup_WhenUserIsOwner()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var oldToken = Guid.NewGuid(); // Guardamos el token original para comparar

        var group = new Group
        {
            Id = groupId,
            InviteToken = oldToken,
            Participants = new List<Participant>
            {
                // El usuario sí es el Owner
                new Participant { UserId = currentUserId, Role = GroupRole.Owner }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        var newToken = await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        // 1. Verificamos que el token devuelto sea distinto al original (¡La magia funcionó!)
        newToken.Should().NotBeEmpty();
        newToken.Should().NotBe(oldToken);

        // 2. Verificamos que el objeto group en memoria se haya actualizado con el nuevo token
        group.InviteToken.Should().Be(newToken);

        // 3. Verificamos que se haya mandado a guardar a la base de datos
        _groupRepositoryMock.Verify(
            repo => repo.UpdateAsync(group, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}