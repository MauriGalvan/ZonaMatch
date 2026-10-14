using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupTest;

public class RemoveParticipantTest
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly RemoveParticipant _useCase;

    public RemoveParticipantTest()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new RemoveParticipant(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenGroupDoesNotExist()
    {
        var groupId = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("El grupo no existe.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserIsNotParticipant()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>() // Lista vacía
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("El usuario no es miembro de este grupo.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserHasNoPermissions()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                new Participant { UserId = userIdToRemove, Role = GroupRole.Guest },
                // El usuario actual intenta echar a alguien, pero solo es Guest, no Owner
                new Participant { UserId = currentUserId, Role = GroupRole.Guest }
            }
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(groupId, userIdToRemove, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("No tienes permisos para eliminar a este participante.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveParticipant_WhenUserRemovesThemselves()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var userIdToRemove = currentUserId; // El usuario se borra a sí mismo

        var participantToRemove = new Participant { UserId = userIdToRemove, Role = GroupRole.Guest };
        var group = new Group
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
    public async Task ExecuteAsync_ShouldRemoveParticipant_WhenCurrentUserIsOwner()
    {
        var groupId = Guid.NewGuid();
        var userIdToRemove = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var participantToRemove = new Participant { UserId = userIdToRemove, Role = GroupRole.Guest };
        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                participantToRemove, // El que va a ser expulsado (Guest)
                new Participant { UserId = currentUserId, Role = GroupRole.Owner } // El que expulsa (Owner)
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