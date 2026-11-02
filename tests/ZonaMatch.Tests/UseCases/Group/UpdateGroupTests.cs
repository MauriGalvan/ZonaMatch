using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class UpdateGroupTests
{
    [Fact]
    public async Task UpdateGroup_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = () => new UpdateGroup(repository.Object).ExecuteAsync(
            groupId, Guid.NewGuid(), new UpdateGroupRequest { Name = "Updated" }, CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.UpdateAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGroup_WhenCurrentUserIsNotOwner_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Before",
            Participants = [new Participant { UserId = userId, Role = GroupRole.Guest }]
        };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        Func<Task> action = () => new UpdateGroup(repository.Object).ExecuteAsync(
            groupId, userId, new UpdateGroupRequest { Name = "After" }, CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>();
        group.Name.Should().Be("Before");
        repository.Verify(item => item.UpdateAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGroup_WhenCurrentUserIsOwner_UpdatesTheName()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Before",
            Participants = [new Participant { UserId = userId, Role = GroupRole.Owner }]
        };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        await new UpdateGroup(repository.Object).ExecuteAsync(
            groupId, userId, new UpdateGroupRequest { Name = "After" }, CancellationToken.None);

        group.Name.Should().Be("After");
        repository.Verify(item => item.UpdateAsync(group, It.IsAny<CancellationToken>()), Times.Once);
    }
}
