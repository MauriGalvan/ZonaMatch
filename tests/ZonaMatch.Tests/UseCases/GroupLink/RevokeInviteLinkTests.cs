using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLink;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.GroupLink;

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
    public async Task RevokeInviteLink_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task RevokeInviteLink_WhenUserIsNotOwner_ThrowsForbiddenException()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                // The user is a Member or Guest, not the Owner.
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Solo el propietario puede revocar el enlace de invitación.");
    }

    [Fact]
    public async Task RevokeInviteLink_WhenUserIsOwner_GeneratesAndPersistsNewToken()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var oldToken = Guid.NewGuid(); // Keep the original token for comparison.

        var group = new DomainGroup
        {
            Id = groupId,
            InviteToken = oldToken,
            Participants = new List<Participant>
            {
                // The user is the Owner.
                new Participant { UserId = currentUserId, Role = GroupRole.Owner }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        var newToken = await _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        // Assert
        // 1. Verify that the returned token differs from the original.
        newToken.Should().NotBeEmpty();
        newToken.Should().NotBe(oldToken);

        // 2. Verify that the in-memory group was updated with the new token.
        group.InviteToken.Should().Be(newToken);

        // 3. Verify that the group was saved to the database.
        _groupRepositoryMock.Verify(
            repo => repo.UpdateAsync(group, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
