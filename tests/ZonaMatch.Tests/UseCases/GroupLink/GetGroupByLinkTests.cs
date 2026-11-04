using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLink;
using ZonaMatch.Domain.Entities;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.GroupLink;

public class GetGroupByLinkTests
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly GetGroupByLink _useCase;

    public GetGroupByLinkTests()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new GetGroupByLink(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task GetGroupByLink_WhenTokenIsInvalidOrRevoked_ThrowsNotFoundException()
    {
        // Arrange
        var token = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        // Act
        Func<Task> action = async () => await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("El enlace de invitación no es válido o ha sido revocado.");
    }

    [Fact]
    public async Task GetGroupByLink_WhenTokenIsValid_ReturnsGroupDetails()
    {
        // Arrange
        var token = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var groupName = "Equipo Alpha";

        var group = new DomainGroup
        {
            Id = groupId,
            Name = groupName,
            InviteToken = token
        };

        _groupRepositoryMock
            .Setup(repo => repo.GetByInviteTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        var result = await _useCase.ExecuteAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GroupId.Should().Be(groupId);
        result.GroupName.Should().Be(groupName);
    }
}
