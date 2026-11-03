using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class CreateGroupTests
{
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly CreateGroup _useCase;

    public CreateGroupTests()
    {
        _groupRepositoryMock = new Mock<IGroupRepository>();
        _useCase = new CreateGroup(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateGroup_WhenRequestIsValid_PersistsGroupAndReturnsResponse()
    {
        var request = new CreateGroupRequest { Name = "Weekend search" };
        var userId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        DomainGroup? persistedGroup = null;

        _groupRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<DomainGroup>(), cancellationToken))
            .Callback<DomainGroup, CancellationToken>((group, _) => persistedGroup = group)
            .Returns(Task.CompletedTask);

        var response = await _useCase.ExecuteAsync(request, userId, cancellationToken);

        persistedGroup.Should().NotBeNull();
        var savedGroup = persistedGroup!;
        savedGroup.Name.Should().Be(request.Name);
        savedGroup.CreatedAt.Should().NotBe(default);
        savedGroup.Participants.Should().ContainSingle(participant =>
            participant.UserId == userId && participant.Role == GroupRole.Owner);

        response.Id.Should().Be(savedGroup.Id);
        response.Name.Should().Be(request.Name);
        response.CreatedAt.Should().Be(savedGroup.CreatedAt);
        response.IsOwner.Should().BeTrue();

        _groupRepositoryMock.Verify(
            repository => repository.AddAsync(savedGroup, cancellationToken),
            Times.Once);
    }
}
