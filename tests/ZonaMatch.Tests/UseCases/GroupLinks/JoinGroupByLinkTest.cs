using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLinks;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupLinks;

public class JoinGroupByLinkTests
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly JoinGroupByLink _useCase;

    public JoinGroupByLinkTests()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new JoinGroupByLink(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenTokenIsInvalidOrRevoked()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null); // Simulamos que no encontró el grupo

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("El enlace de invitación no es válido o ha sido revocado.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserIsAlreadyMember()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            InviteToken = token,
            Participants = new List<Participant>
            {
                // El usuario actual YA está en la lista de participantes del grupo
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("Ya eres miembro de este grupo.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddParticipant_WhenTokenIsValidAndUserIsNotMember()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            InviteToken = token,
            Participants = new List<Participant>() // La lista está vacía, el usuario NO es miembro
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        // Verificamos que se haya llamado al método de inserción directa con los datos correctos
        _groupRepositoryMock.Verify(
            repo => repo.AddParticipantAsync(
                It.Is<Participant>(p =>
                    p.GroupId == groupId &&
                    p.UserId == currentUserId &&
                    p.Role == GroupRole.Guest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}