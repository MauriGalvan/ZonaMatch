using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainGroup = ZonaMatch.Domain.Entities.Group;
using DomainCriterion = ZonaMatch.Domain.Entities.Criterion;

namespace ZonaMatch.Tests.UseCases.Group;

public class GetGroupDetailsTests
{
    [Fact]
    public async Task GetGroupDetails_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var groupRepository = new Mock<IGroupRepository>();
        groupRepository.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = () => new GetGroupDetails(
            groupRepository.Object,
            new Mock<IProfileRepository>().Object,
            new Mock<ICriterionRepository>().Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetGroupDetails_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var groupRepository = new Mock<IGroupRepository>();
        groupRepository.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomainGroup { Id = groupId, Name = "Weekend search" });

        Func<Task> action = () => new GetGroupDetails(
            groupRepository.Object,
            new Mock<IProfileRepository>().Object,
            new Mock<ICriterionRepository>().Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetGroupDetails_WhenUserIsParticipant_ReturnsTheirProfileAndActiveCriteria()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participant = new Participant
        {
            UserId = userId,
            Role = GroupRole.Owner,
            User = new User { Id = userId, Email = "ana.torres@example.com" }
        };
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Weekend search",
            CreatedAt = new DateTime(2026, 10, 1),
            Participants = [participant]
        };
        var profile = new Context
        {
            UserId = userId,
            MaximumRent = "500000",
            Criteria = [new ContextCriterion { CriterionId = "safety", Priority = "alta" }],
            Places = [new ContextPlace { Name = "Work", Address = "Main Street", Order = 0, MaximumTravelTime = "30 min" }]
        };
        var groupRepository = new Mock<IGroupRepository>();
        groupRepository.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        var profileRepository = new Mock<IProfileRepository>();
        profileRepository.Setup(repository => repository.GetByUserIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(userId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profile });
        var criteriaRepository = new Mock<ICriterionRepository>();
        criteriaRepository.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DomainCriterion { Id = "safety", Name = "Safety", IsActive = true },
                new DomainCriterion { Id = "inactive", Name = "Inactive", IsActive = false }
            });

        var response = await new GetGroupDetails(
                groupRepository.Object, profileRepository.Object, criteriaRepository.Object)
            .ExecuteAsync(groupId, userId, CancellationToken.None);

        var resultParticipant = response.Participants.Should().ContainSingle().Which;
        resultParticipant.Name.Should().Be("ana.torres@example.com");
        resultParticipant.IsMe.Should().BeTrue();
        resultParticipant.Initials.Should().Be("AT");
        resultParticipant.Preferences.Should().ContainKey("safety").WhoseValue.Should().Be("Alta");
        resultParticipant.Points.Should().ContainSingle().Which.Name.Should().Be("Work");
        response.CriteriaList.Should().ContainSingle().Which.Key.Should().Be("safety");
    }
}
