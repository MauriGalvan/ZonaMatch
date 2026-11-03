using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Domain.Scoring;
using DomainGroup = ZonaMatch.Domain.Entities.Group;

namespace ZonaMatch.Tests.UseCases.Group;

public class CalculateGroupScoreTests
{
    [Fact]
    public async Task CalculateGroupScore_WhenGroupDoesNotExist_ThrowsNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGroup?)null);

        Func<Task> action = () => new CalculateGroupScore(
                groups.Object,
                new Mock<IProfileRepository>().Object,
                new Mock<IZoneProfilesRepository>().Object,
                new Mock<IRoutingClient>().Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), new CalculateGroupScoreRequest());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenCurrentUserIsNotParticipant_ThrowsForbiddenException()
    {
        var groupId = Guid.NewGuid();
        var group = new DomainGroup { Id = groupId, Name = "Weekend search" };
        var groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Func<Task> action = () => new CalculateGroupScore(
                groups.Object,
                new Mock<IProfileRepository>().Object,
                new Mock<IZoneProfilesRepository>().Object,
                new Mock<IRoutingClient>().Object)
            .ExecuteAsync(groupId, Guid.NewGuid(), new CalculateGroupScoreRequest());

        await action.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenWeightsOmitAGroupParticipant_ThrowsBadRequestException()
    {
        var groupId = Guid.NewGuid();
        var firstParticipant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var secondParticipant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Guest };
        var group = new DomainGroup
        {
            Id = groupId,
            Name = "Weekend search",
            Participants = [firstParticipant, secondParticipant]
        };
        var groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = firstParticipant.Id, Weight = 100 }]
        };

        Func<Task> action = () => new CalculateGroupScore(
                groups.Object,
                new Mock<IProfileRepository>().Object,
                new Mock<IZoneProfilesRepository>().Object,
                new Mock<IRoutingClient>().Object)
            .ExecuteAsync(groupId, firstParticipant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CalculateGroupScore_WhenParticipantWeightIsOutsideAllowedRange_ThrowsBadRequestException(int weight)
    {
        var participant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = weight }]
        };

        Func<Task> action = () => CreateUseCase(group).ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenWeightReferencesSomeoneOutsideTheGroup_ThrowsBadRequestException()
    {
        var participant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = Guid.NewGuid(), Weight = 100 }]
        };

        Func<Task> action = () => CreateUseCase(group).ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenParticipantWeightsContainDuplicates_ThrowsBadRequestException()
    {
        var first = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var second = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Guest };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [first, second] };
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights =
            [
                new ParticipantWeightRequest { ParticipantId = first.Id, Weight = 50 },
                new ParticipantWeightRequest { ParticipantId = first.Id, Weight = 50 }
            ]
        };

        Func<Task> action = () => CreateUseCase(group).ExecuteAsync(group.Id, first.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenAllParticipantWeightsAreZero_ThrowsBadRequestException()
    {
        var participant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 0 }]
        };

        Func<Task> action = () => CreateUseCase(group).ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenAddedZoneDoesNotExist_ThrowsNotFoundException()
    {
        var participant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ZoneProfile { Slug = "safe-zone", Name = "Safe Zone", Level = 9 } });
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }],
            AddedZones = ["missing-zone"]
        };

        Func<Task> action = () => CreateUseCase(group, zoneProfiles: zoneProfiles)
            .ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenNoParticipantHasAProfile_ThrowsBadRequestException()
    {
        var participant = new Participant { UserId = Guid.NewGuid(), Role = GroupRole.Owner };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(userIds => userIds.Contains(participant.UserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Context>());
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ZoneProfile { Slug = "safe-zone", Name = "Safe Zone", Level = 9 } });
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }]
        };

        Func<Task> action = () => CreateUseCase(group, profiles, zoneProfiles)
            .ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CalculateGroupScore_WhenPlacesHaveLocations_RanksOnlyRepositoryCandidateZones()
    {
        var participant = new Participant
        {
            UserId = Guid.NewGuid(),
            Role = GroupRole.Owner,
            User = new User { Email = "ana@example.com" }
        };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var profile = new Context
        {
            UserId = participant.UserId,
            Criteria = [new ContextCriterion { CriterionId = ScoringEngine.Safety, Priority = "alta" }],
            Places = [new ContextPlace { Name = "Work", Latitude = -34.6, Longitude = -58.4, MaximumTravelTime = "30 min" }]
        };
        var profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profile });
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Zone("point-zone", 10),
                Zone("neighbor-zone", 20),
                Zone("far-zone", 100)
            });
        zoneProfiles.Setup(repository => repository.ListNearbyZoneSlugsAsync(
                It.IsAny<IReadOnlyCollection<(double Latitude, double Longitude)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "point-zone", "neighbor-zone" });
        var routing = new Mock<IRoutingClient>();
        routing.Setup(client => client.GetTravelTimeAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelTime(20, false, 2));
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }]
        };

        var result = await CreateUseCase(group, profiles, zoneProfiles, routing)
            .ExecuteAsync(group.Id, participant.UserId, request);

        var rankedSlugs = result.Zones.Select(zone => zone.Slug).ToArray();
        rankedSlugs.Should().HaveCount(2);
        rankedSlugs.Should().Contain("point-zone");
        rankedSlugs.Should().Contain("neighbor-zone");
        rankedSlugs.Should().NotContain("far-zone");
        zoneProfiles.Verify(repository => repository.ListNearbyZoneSlugsAsync(
            It.Is<IReadOnlyCollection<(double Latitude, double Longitude)>>(locations =>
                locations.Count == 1
                && locations.Single().Latitude == -34.6
                && locations.Single().Longitude == -58.4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateGroupScore_WhenNoCandidateZoneContainsThePlaces_ThrowsBadRequestBeforeRouting()
    {
        var participant = new Participant
        {
            UserId = Guid.NewGuid(),
            Role = GroupRole.Owner,
            User = new User { Email = "ana@example.com" }
        };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [participant] };
        var profile = new Context
        {
            UserId = participant.UserId,
            Places = [new ContextPlace { Name = "Work", Latitude = -34.6, Longitude = -58.4 }]
        };
        var profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profile });
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Zone("far-zone", 10) });
        zoneProfiles.Setup(repository => repository.ListNearbyZoneSlugsAsync(
                It.IsAny<IReadOnlyCollection<(double Latitude, double Longitude)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var routing = new Mock<IRoutingClient>();
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }]
        };

        Func<Task> action = () => CreateUseCase(group, profiles, zoneProfiles, routing)
            .ExecuteAsync(group.Id, participant.UserId, request);

        await action.Should().ThrowAsync<BadRequestException>();
        routing.Verify(client => client.GetTravelTimeAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculateGroupScore_WhenParticipantsHaveProfiles_RanksZonesByWeightedScore()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Owner,
            User = new User { Id = userId, Email = "ana@example.com" }
        };
        var group = new DomainGroup { Id = groupId, Name = "Weekend search", Participants = [participant] };
        var profile = new Context
        {
            UserId = userId,
            Criteria = [new ContextCriterion { CriterionId = ScoringEngine.Safety, Priority = "alta" }]
        };
        var groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        var profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(userId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profile });
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[]
            {
                new() { Slug = "unsafe-zone", Name = "Unsafe Zone", Level = 9, Population = 1_000, CrimeCount = 100 },
                new() { Slug = "safe-zone", Name = "Safe Zone", Level = 9, Population = 1_000, CrimeCount = 10 }
            });
        var request = new CalculateGroupScoreRequest
        {
            ParticipantWeights = [new ParticipantWeightRequest { ParticipantId = participant.Id, Weight = 100 }]
        };

        var result = await new CalculateGroupScore(
                groups.Object, profiles.Object, zoneProfiles.Object, new Mock<IRoutingClient>().Object)
            .ExecuteAsync(groupId, userId, request);

        result.Zones.Select(zone => zone.Slug).Should().Equal("safe-zone", "unsafe-zone");
        result.Zones.First().Score!.Value.Should().BeGreaterThan(result.Zones.Last().Score!.Value);
        result.Zones.Should().OnlyContain(zone => zone.Participants.Should().ContainSingle().Which.Weight == 100);
    }

    [Fact]
    public async Task CalculateGroupScore_ChangesZoneRankingWhenParticipantWeightsChange()
    {
        var owner = new Participant
        {
            UserId = Guid.NewGuid(),
            Role = GroupRole.Owner,
            User = new User { Email = "ana@example.com" }
        };
        var guest = new Participant
        {
            UserId = Guid.NewGuid(),
            Role = GroupRole.Guest,
            User = new User { Email = "luis@example.com" }
        };
        var group = new DomainGroup { Id = Guid.NewGuid(), Participants = [owner, guest] };
        var profiles = new Mock<IProfileRepository>();
        profiles.Setup(repository => repository.GetByUserIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Context
                {
                    UserId = owner.UserId,
                    Criteria = [new ContextCriterion { CriterionId = ScoringEngine.Safety, Priority = "alta" }]
                },
                new Context
                {
                    UserId = guest.UserId,
                    Criteria = [new ContextCriterion { CriterionId = ScoringEngine.Education, Priority = "alta" }]
                }
            });
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        zoneProfiles.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ZoneProfile { Slug = "safe-zone", Name = "Safe Zone", Level = 9, Population = 1_000, CrimeCount = 10, SchoolCount = 0 },
                new ZoneProfile { Slug = "school-zone", Name = "School Zone", Level = 9, Population = 1_000, CrimeCount = 100, SchoolCount = 100 }
            });
        var useCase = CreateUseCase(group, profiles, zoneProfiles);

        var safetyWeighted = await useCase.ExecuteAsync(group.Id, owner.UserId, new CalculateGroupScoreRequest
        {
            ParticipantWeights =
            [
                new ParticipantWeightRequest { ParticipantId = owner.Id, Weight = 100 },
                new ParticipantWeightRequest { ParticipantId = guest.Id, Weight = 0 }
            ]
        });
        var educationWeighted = await useCase.ExecuteAsync(group.Id, owner.UserId, new CalculateGroupScoreRequest
        {
            ParticipantWeights =
            [
                new ParticipantWeightRequest { ParticipantId = owner.Id, Weight = 0 },
                new ParticipantWeightRequest { ParticipantId = guest.Id, Weight = 100 }
            ]
        });

        safetyWeighted.Zones.Single(zone => zone.Slug == "safe-zone").Score!.Value.Should()
            .BeGreaterThan(safetyWeighted.Zones.Single(zone => zone.Slug == "school-zone").Score!.Value);
        educationWeighted.Zones.Single(zone => zone.Slug == "school-zone").Score!.Value.Should()
            .BeGreaterThan(educationWeighted.Zones.Single(zone => zone.Slug == "safe-zone").Score!.Value);
    }

    private static CalculateGroupScore CreateUseCase(
        DomainGroup group,
        Mock<IProfileRepository>? profiles = null,
        Mock<IZoneProfilesRepository>? zoneProfiles = null,
        Mock<IRoutingClient>? routing = null)
    {
        var groups = new Mock<IGroupRepository>();
        groups.Setup(repository => repository.GetDetailsByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        return new CalculateGroupScore(
            groups.Object,
            (profiles ?? new Mock<IProfileRepository>()).Object,
            (zoneProfiles ?? new Mock<IZoneProfilesRepository>()).Object,
            (routing ?? new Mock<IRoutingClient>()).Object);
    }

    private static ZoneProfile Zone(string slug, int crimeCount) => new()
    {
        Slug = slug,
        Name = slug,
        Level = 9,
        Population = 1_000,
        CrimeCount = crimeCount,
        Latitude = -34.6,
        Longitude = -58.5
    };
}
