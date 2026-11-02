using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class DeleteGroupTests
{
    private readonly Mock<IGroupRepository> _groupRepository = new();
    private readonly DeleteGroup _useCase;

    public DeleteGroupTests() => _useCase = new DeleteGroup(_groupRepository.Object);

    [Fact]
    public async Task DeleteGroup_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        _groupRepository.Setup(repository => repository.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = () => _useCase.ExecuteAsync(groupId, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
        _groupRepository.Verify(repository => repository.DeleteAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGroup_WhenCurrentUserIsNotOwner_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Participants = [new Participant { UserId = currentUserId, Role = GroupRole.Guest }]
        };
        _groupRepository.Setup(repository => repository.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = () => _useCase.ExecuteAsync(groupId, currentUserId, CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>();
        _groupRepository.Verify(repository => repository.DeleteAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGroup_WhenCurrentUserIsOwner_DeletesTheGroup()
    {
        var groupId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Participants = [new Participant { UserId = ownerId, Role = GroupRole.Owner }]
        };
        _groupRepository.Setup(repository => repository.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _useCase.ExecuteAsync(groupId, ownerId, CancellationToken.None);

        _groupRepository.Verify(repository => repository.DeleteAsync(group, It.IsAny<CancellationToken>()), Times.Once);
    }
}
