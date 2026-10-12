using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Tests.Application.UseCases.GroupInvitations;

public class GetInvitationByTokenTests
{
    private readonly Mock<IGroupInvitationRepository> _invitationRepositoryMock;
    private readonly GetInvitationByToken _useCase;

    public GetInvitationByTokenTests()
    {
        _invitationRepositoryMock = new Mock<IGroupInvitationRepository>();
        _useCase = new GetInvitationByToken(_invitationRepositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowException_WhenInvitationDoesNotExist()
    {
        // Arrange
        var token = Guid.NewGuid();

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInvitation?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<Exception>()
            .WithMessage("La invitación no existe.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvitationDetails_WhenInvitationExists()
    {
        // Arrange
        var token = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(3);

        var invitation = new GroupInvitation
        {
            Token = token,
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = expirationDate,
            Group = new Group
            {
                Id = groupId,
                Name = "Grupo de Pruebas"
            } // ¡Clave! Instanciamos el grupo anidado para simular el .Include() de EF Core
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        // Act
        var result = await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verificamos que el mapeo hacia el DTO se haya hecho correctamente
        result.GroupId.Should().Be(groupId);
        result.GroupName.Should().Be("Grupo de Pruebas");
        result.Status.Should().Be("Pending");
        result.ExpiresAt.Should().Be(expirationDate);
    }
}