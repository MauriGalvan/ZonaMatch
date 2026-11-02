using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class GetGroupsTests
{
    [Fact]
    public async Task GetGroups_ReturnsGroupsAndMarksOwnershipForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var ownedGroup = new DomainGroup
        {
            Name = "Owned",
            Participants = [new Participant { UserId = userId, Role = GroupRole.Owner }]
        };
        var joinedGroup = new DomainGroup
        {
            Name = "Joined",
            Participants = [new Participant { UserId = userId, Role = GroupRole.Guest }]
        };
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByParticipantUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ownedGroup, joinedGroup });

        var response = await new GetGroups(repository.Object).ExecuteAsync(userId, CancellationToken.None);

        response.Groups.Should().HaveCount(2);
        response.Groups.Select(group => group.Name).Should().Equal("Owned", "Joined");
        response.Groups.Select(group => group.IsOwner).Should().Equal(true, false);
    }

    [Fact]
    public async Task GetGroups_WhenUserHasNoGroups_ReturnsAnEmptyList()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IGroupRepository>();
        repository.Setup(item => item.GetByParticipantUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DomainGroup>());

        var response = await new GetGroups(repository.Object).ExecuteAsync(userId, CancellationToken.None);

        response.Groups.Should().BeEmpty();
    }
}
