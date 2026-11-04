using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Domain.Scoring;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class SaveGroupAnalysisTests
{
    [Fact]
    public async Task SaveGroupAnalysis_WhenNameIsBlank_ThrowsBadRequestBeforeCalculating()
    {
        var calculator = CreateCalculator(
            out _, out _, out _, out _, out var profiles, out _, out _);
        var useCase = new SaveGroupAnalysis(
            calculator,
            new Mock<IAnalysisRepository>().Object,
            profiles.Object,
            new Mock<IZoneRepository>().Object,
            TimeProvider.System);

        Func<Task> action = () => useCase.ExecuteAsync(
            Guid.NewGuid(), Guid.NewGuid(), new SaveGroupAnalysisRequest { Name = "  " });

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task SaveGroupAnalysis_WhenRequestIsValid_SavesTrimmedNameAndRankingSnapshot()
    {
        var calculator = CreateCalculator(
            out var groupId, out var userId, out var participant, out var zoneProfiles, out var profiles,
            out var groupRepository, out var profileContext);
        var analysisRepository = new Mock<IAnalysisRepository>();
        Analysis? savedAnalysis = null;
        analysisRepository.Setup(repository => repository.Add(It.IsAny<Analysis>()))
            .Callback<Analysis>(analysis => savedAnalysis = analysis);
        var zoneRepository = new Mock<IZoneRepository>();
        zoneRepository.Setup(repository => repository.ExistsAsync("safe-zone", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var now = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
        var request = new SaveGroupAnalysisRequest
        {
            Name = "  Shared search  ",
            Zones = [new AnalysisZoneRequest { Slug = "safe-zone", Name = "Safe Zone", IsAdded = true }],
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }]
        };
        var useCase = new SaveGroupAnalysis(
            calculator, analysisRepository.Object, profiles.Object, zoneRepository.Object, new FixedTimeProvider(now));

        var response = await useCase.ExecuteAsync(groupId, userId, request);

        savedAnalysis.Should().NotBeNull();
        savedAnalysis!.Name.Should().Be("Shared search");
        savedAnalysis.GroupScoresJson.Should().NotBeNullOrWhiteSpace();
        savedAnalysis.Zones.Should().ContainSingle().Which.ZoneSlug.Should().Be("safe-zone");
        savedAnalysis.Context.Type.Should().Be(ContextType.Analysis);
        savedAnalysis.CreatedAt.Should().Be(now);
        response.Name.Should().Be("Shared search");
        response.GroupScores.Should().NotBeNull();
        response.GroupScores!.Zones.Select(zone => zone.Slug).Should().Equal("safe-zone", "unsafe-zone");
        analysisRepository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        zoneProfiles.Verify(repository => repository.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        groupRepository.Verify(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()), Times.Once);
        profileContext.UserId.Should().Be(userId);
    }

    private static CalculateGroupScore CreateCalculator(
        out Guid groupId,
        out Guid userId,
        out Participant participant,
        out Mock<IZoneProfilesRepository> zoneProfiles,
        out Mock<IProfileRepository> profiles,
        out Mock<IGroupRepository> groups,
        out Context profileContext)
    {
        groupId = Guid.NewGuid();
        userId = Guid.NewGuid();
        var setupGroupId = groupId;
        var setupUserId = userId;
        participant = new Participant
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Owner,
            User = new User { Id = userId, Email = "ana@example.com" }
        };
        var group = new DomainGroup { Id = groupId, Name = "Weekend search", Participants = [participant] };
        groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(setupGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        profileContext = new Context
        {
            UserId = userId,
            Criteria = [new ContextCriterion { CriterionId = ScoringEngine.Safety, Priority = "alta" }]
        };
        profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(setupUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profileContext });
        profiles.Setup(repository => repository.GetByUserAsync(setupUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Context?)null);
        zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[]
            {
                new() { Slug = "safe-zone", Name = "Safe Zone", Level = 9, Population = 1_000, CrimeCount = 10 },
                new() { Slug = "unsafe-zone", Name = "Unsafe Zone", Level = 9, Population = 1_000, CrimeCount = 100 }
            });

        return new CalculateGroupScore(
            groups.Object,
            profiles.Object,
            zoneProfiles.Object,
            new Mock<IRoutingClient>().Object);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
