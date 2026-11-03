using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GroupLink;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.GroupLink;

public class GetGroupInviteLinkTests
{
    [Fact]
    public async Task GetGroupInviteLink_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = () => new GetGroupInviteLink(repository.Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetGroupInviteLink_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var group = new DomainGroup { Id = groupId, Name = "Group", InviteToken = Guid.NewGuid() };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        Func<Task> action = () => new GetGroupInviteLink(repository.Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(item => item.UpdateAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetGroupInviteLink_WhenLegacyTokenIsEmpty_GeneratesAndPersistsToken()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Group",
            InviteToken = Guid.Empty,
            Participants = [new Participant { UserId = userId, Role = GroupRole.Guest }]
        };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var token = await new GetGroupInviteLink(repository.Object)
            .ExecuteAsync(groupId, userId, CancellationToken.None);

        token.Should().NotBe(Guid.Empty);
        token.Should().Be(group.InviteToken);
        repository.Verify(item => item.UpdateAsync(group, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGroupInviteLink_WhenTokenAlreadyExists_ReturnsItWithoutRotatingIt()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = Guid.NewGuid();
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Group",
            InviteToken = token,
            Participants = [new Participant { UserId = userId, Role = GroupRole.Guest }]
        };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var result = await new GetGroupInviteLink(repository.Object)
            .ExecuteAsync(groupId, userId, CancellationToken.None);

        result.Should().Be(token);
        repository.Verify(item => item.UpdateAsync(It.IsAny<DomainGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
