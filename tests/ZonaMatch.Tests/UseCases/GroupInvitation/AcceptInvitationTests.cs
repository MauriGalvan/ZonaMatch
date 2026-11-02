using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitation;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;
using DomainGroupInvitation = ZonaMatch.Domain.Entities.GroupInvitation;

namespace ZonaMatch.Tests.UseCases.GroupInvitation;

public class AcceptInvitationTests
{
    private readonly Mock<IGroupInvitationRepository> _invitationRepositoryMock;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly AcceptInvitation _useCase;

    public AcceptInvitationTests()
    {
        _invitationRepositoryMock = new Mock<IGroupInvitationRepository>();
        _groupRepositoryMock = new Mock<IGroupRepository>();

        _useCase = new AcceptInvitation(
            _invitationRepositoryMock.Object,
            _groupRepositoryMock.Object);
    }

    [Fact]
    public async Task AcceptInvitation_WhenInvitationDoesNotExist_ThrowsNotFoundException()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroupInvitation?)null);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("Invitación no encontrada.");
    }

    [Fact]
    public async Task AcceptInvitation_WhenInvitationIsNotPending_ThrowsBadRequestException()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var invitation = new DomainGroupInvitation { Status = InvitationStatus.Accepted }; // Invalid state.

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("La invitación ya no es válida (Estado: Accepted).");
    }

    [Fact]
    public async Task AcceptInvitation_WhenInvitationIsExpired_MarksItExpiredAndThrowsBadRequestException()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var invitation = new DomainGroupInvitation
        {
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday.
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Verify that the exception is thrown.
        await action.Should().ThrowAsync<BadRequestException>().WithMessage("La invitación ha expirado.");

        // Also verify that UpdateAsync was called to save the 'Expired' status in the database.
        invitation.Status.Should().Be(InvitationStatus.Expired);
        _invitationRepositoryMock.Verify(
            repo => repo.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptInvitation_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new DomainGroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1) // Valid.
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null); // The group does not exist.

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("El grupo ya no existe.");
    }

    [Fact]
    public async Task AcceptInvitation_WhenUserIsAlreadyMember_ThrowsConflictException()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new DomainGroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                new Participant { UserId = currentUserId, Role = GroupRole.Guest } // The user is already a member.
            }
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>().WithMessage("Ya eres miembro de este grupo.");
    }

    [Fact]
    public async Task AcceptInvitation_WhenGroupIsFull_ThrowsConflictWithoutAcceptingInvitation()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var invitation = new DomainGroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var group = new DomainGroup
        {
            Id = groupId,
            Participants = Enumerable.Range(0, DomainGroup.MaximumParticipants)
                .Select(_ => new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Guest })
                .ToList()
        };
        _invitationRepositoryMock
            .Setup(repository => repository.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _groupRepositoryMock
            .Setup(repository => repository.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = () => _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        invitation.Status.Should().Be(InvitationStatus.Pending);
        _groupRepositoryMock.Verify(
            repository => repository.AddParticipantAsync(It.IsAny<Participant>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _invitationRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<DomainGroupInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptInvitation_WhenInvitationIsValid_UpdatesInvitationAndAddsParticipant()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new DomainGroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>() // Empty group.
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Assert
        // 1. Verify that the status changed to Accepted and was updated in the database.
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        _invitationRepositoryMock.Verify(
            repo => repo.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);

        // 2. Verify that the participant was inserted into the database.
        _groupRepositoryMock.Verify(
            repo => repo.AddParticipantAsync(
                It.Is<Participant>(p => p.GroupId == groupId && p.UserId == currentUserId && p.Role == GroupRole.Guest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
