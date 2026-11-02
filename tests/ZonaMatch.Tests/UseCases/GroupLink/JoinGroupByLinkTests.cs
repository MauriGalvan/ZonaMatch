using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLink;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.GroupLink;

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
    public async Task JoinGroupByLink_WhenTokenIsInvalidOrRevoked_ThrowsNotFoundException()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null); // Simulate that the group was not found.

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("El enlace de invitación no es válido o ha sido revocado.");
    }

    [Fact]
    public async Task JoinGroupByLink_WhenUserIsAlreadyMember_ThrowsConflictException()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = new DomainGroup
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
        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage("Ya eres miembro de este grupo.");
    }

    [Fact]
    public async Task JoinGroupByLink_WhenGroupIsFull_ThrowsConflictWithoutAddingParticipant()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = Guid.NewGuid(),
            InviteToken = token,
            Participants = Enumerable.Range(0, DomainGroup.MaximumParticipants)
                .Select(_ => new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Guest })
                .ToList()
        };
        _groupRepositoryMock
            .Setup(repository => repository.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = () => _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        _groupRepositoryMock.Verify(
            repository => repository.AddParticipantAsync(It.IsAny<Participant>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinGroupByLink_WhenTokenIsValidAndUserIsNotMember_AddsParticipant()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var group = new DomainGroup
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
