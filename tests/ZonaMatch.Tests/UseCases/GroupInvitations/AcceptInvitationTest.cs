using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupInvitations;

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
    public async Task ExecuteAsync_ShouldThrowException_WhenInvitationNotFound()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInvitation?)null);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("Invitación no encontrada.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenStatusIsNotPending()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var invitation = new GroupInvitation { Status = InvitationStatus.Accepted }; // Estado inválido

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>()
            .WithMessage("La invitación ya no es válida (Estado: Accepted).");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkAsExpiredAndThrowException_WhenInvitationIsExpired()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var invitation = new GroupInvitation
        {
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expiró ayer
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        // Verificamos que lanza la excepción
        await action.Should().ThrowAsync<Exception>().WithMessage("La invitación ha expirado.");

        // ADEMÁS: Verificamos que se haya llamado al UpdateAsync para guardar el estado 'Expired' en BD
        invitation.Status.Should().Be(InvitationStatus.Expired);
        _invitationRepositoryMock.Verify(
            repo => repo.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenGroupNotFound()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new GroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1) // Válida
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null); // El grupo no existe

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("El grupo ya no existe.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenUserAlreadyMember()
    {
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new GroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>
            {
                new Participant { UserId = currentUserId, Role = GroupRole.Guest } // El usuario ya está adentro
            }
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        _groupRepositoryMock
            .Setup(repo => repo.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = async () => await _useCase.ExecuteAsync(token, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<Exception>().WithMessage("Ya eres miembro de este grupo.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAcceptInvitationAndAddParticipant_WhenValid()
    {
        // Arrange
        var token = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var invitation = new GroupInvitation
        {
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var group = new Group
        {
            Id = groupId,
            Participants = new List<Participant>() // Grupo vacío
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
        // 1. Verificamos que el estado cambió a Accepted y se actualizó en la BD
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        _invitationRepositoryMock.Verify(
            repo => repo.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);

        // 2. Verificamos que se insertó el participante en la BD
        _groupRepositoryMock.Verify(
            repo => repo.AddParticipantAsync(
                It.Is<Participant>(p => p.GroupId == groupId && p.UserId == currentUserId && p.Role == GroupRole.Guest),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}