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
    public async Task GetInvitationByToken_WhenInvitationDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var token = Guid.NewGuid();

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroupInvitation?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("La invitación no existe.");
    }

    [Fact]
    public async Task GetInvitationByToken_WhenInvitationExists_ReturnsInvitationDetails()
    {
        // Arrange
        var token = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(3);

        var invitation = new DomainGroupInvitation
        {
            Token = token,
            GroupId = groupId,
            Status = InvitationStatus.Pending,
            ExpiresAt = expirationDate,
            Group = new DomainGroup
            {
                Id = groupId,
                Name = "Grupo de Pruebas"
            } // Create the nested group to simulate the EF Core .Include().
        };

        _invitationRepositoryMock
            .Setup(repo => repo.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        // Act
        var result = await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify that the DTO mapping was performed correctly.
        result.GroupId.Should().Be(groupId);
        result.GroupName.Should().Be("Grupo de Pruebas");
        result.Status.Should().Be("Pending");
        result.ExpiresAt.Should().Be(expirationDate);
    }
}
