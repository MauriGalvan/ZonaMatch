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
            .ReturnsAsync((Group?)null); // Simulate that the group was not found.

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
                // The current user is already in the group's participant list.
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
            Participants = new List<Participant>() // The list is empty; the user is not a member.
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        // Verify that the direct insert method was called with the expected data.
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
