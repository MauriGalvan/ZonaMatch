using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class RemoveParticipantTests
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly RemoveParticipant _useCase;

    public RemoveParticipantTests()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new RemoveParticipant(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task RemoveParticipant_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task RemoveParticipant_WhenTargetIsNotParticipant_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>() // Empty list.
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("El usuario no es miembro de este grupo.");
    }

    [Fact]
    public async Task RemoveParticipant_WhenCurrentUserIsNotOwnerOrTarget_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                new Participant { UserId = userIdToRemove, Role = GroupRole.Guest },
                // The current user tries to remove someone but is a Guest, not the Owner.
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>().WithMessage("No tienes permisos para eliminar a este participante.");
    }

    [Fact]
    public async Task RemoveParticipant_WhenUserLeavesTheirOwnGroup_RemovesParticipant()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var userIdToRemove = currentUserId; // The user removes themselves.

        var participantToRemove = new Participant { UserId = userIdToRemove, Role = GroupRole.Guest };
        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant> { participantToRemove }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        _groupRepositoryMock.Verify(
            repo => repo.RemoveParticipantAsync(participantToRemove, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveParticipant_WhenOwnerRemovesAnotherParticipant_RemovesParticipant()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var participantToRemove = new Participant { UserId = userIdToRemove, Role = GroupRole.Guest };
        var group = new DomainGroup
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                participantToRemove, // The participant being removed (Guest).
                new Participant { UserId = currentUserId, Role = GroupRole.Owner } // The participant doing the removal (Owner).
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        _groupRepositoryMock.Verify(
            repo => repo.RemoveParticipantAsync(participantToRemove, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
