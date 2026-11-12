using FluentAssertions;
using Moq;
using System.Text.Json;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public class CompareAnalysisDistanceTests
{
    [Fact]
    public async Task CompareAnalyses_WhenSavedPlaceWasByBicycle_PreservesBicycleFlag()
    {
        var userId = Guid.NewGuid();
        var savedResult = JsonSerializer.Serialize(
            new ScoresResponse("auto", [new ScoredZoneResponse(
                "recoleta", "Recoleta", 82, [], [],
                [new PlaceTravelTimeResponse("Trabajo", 20, 45, false, 8.75, ByBicycle: true)],
                null)], []),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Analysis {index}",
            ResultJson = savedResult,
            Zones = [new AnalysisZone { ZoneSlug = "recoleta", Name = "Recoleta", Order = 1 }]
        }).ToArray();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);

        var response = await new CompareAnalyses(
                repository.Object,
                new Mock<IZoneProfilesRepository>().Object,
                new Mock<IRoutingClient>().Object)
            .ExecuteAsync(userId, new CompareAnalysesRequest
            {
                AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
            });

        response.Analyses.Should().AllSatisfy(analysis =>
            analysis.Zones.Should().ContainSingle().Which.Places.Should().ContainSingle().Which.ByBicycle.Should().BeTrue());
    }

    [Fact]
    public async Task CompareAnalyses_WhenPlacesHaveRouteDistances_PreservesDistanceInEachAnalysis()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(number => new Analysis
        {
            UserId = userId,
            Name = $"Busqueda {number}",
            Zones = [new AnalysisZone { ZoneSlug = "recoleta", Name = "Recoleta", Order = 1 }],
            Context = new Context
            {
                UserId = userId,
                Mobility = ["vehiculo"],
                Places = [new ContextPlace { Name = "Trabajo", Latitude = -34.6, Longitude = -58.4, MaximumTravelTime = "45 min" }]
            }
        }).ToArray();

        var analysisRepository = new Mock<IAnalysisRepository>();
        analysisRepository.Setup(repository => repository.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
        var zoneProfilesRepository = new Mock<IZoneProfilesRepository>();
        zoneProfilesRepository.Setup(repository => repository.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[] { new() { Slug = "recoleta", Name = "Recoleta", Level = 9 } });
        var routingClient = new Mock<IRoutingClient>();
        routingClient.Setup(client => client.GetTravelTimeAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                TravelMode.Car, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelTime(25, false, 8.75));

        var result = await new CompareAnalyses(analysisRepository.Object, zoneProfilesRepository.Object, routingClient.Object)
            .ExecuteAsync(userId, new CompareAnalysesRequest { AnalysisIds = analyses.Select(analysis => analysis.Id).ToList() });

        result.Analyses.Should().HaveCount(2);
        result.Analyses.Should().AllSatisfy(comparedAnalysis =>
        {
            var zone = comparedAnalysis.Zones.Should().ContainSingle().Which;
            var place = zone.Places.Should().ContainSingle().Which;
            place.Minutes.Should().Be(25);
            place.DistanceKm!.Value.Should().BeApproximately(8.75, 0.001);
        });
    }

    [Fact]
    public async Task CompareAnalyses_WhenFewerThanTwoIdsAreProvided_ThrowsBadRequestException()
    {
        var useCase = new CompareAnalyses(
            new Mock<IAnalysisRepository>().Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(Guid.NewGuid(), new CompareAnalysesRequest());

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CompareAnalyses_WhenMoreThanFiveIdsAreProvided_ThrowsBadRequestBeforeLoadingAnalyses()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IAnalysisRepository>();
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Elegí entre dos y cinco análisis para comparar.");
        repository.Verify(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompareAnalyses_WhenIdsAreRepeated_ThrowsBadRequestException()
    {
        var analysisId = Guid.NewGuid();
        var useCase = new CompareAnalyses(
            new Mock<IAnalysisRepository>().Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(Guid.NewGuid(), new CompareAnalysesRequest
        {
            AnalysisIds = [analysisId, analysisId]
        });

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CompareAnalyses_WhenAnalysisIsNotOwnedByUser_ThrowsNotFoundException()
    {
        var userId = Guid.NewGuid();
        var analysisRepository = new Mock<IAnalysisRepository>();
        analysisRepository.Setup(repository => repository.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var useCase = new CompareAnalyses(
            analysisRepository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = [Guid.NewGuid(), Guid.NewGuid()]
        });

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CompareAnalyses_WhenAnAnalysisHasNoZones_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var analyses = new[]
        {
            new Analysis { UserId = userId, Name = "Empty", Zones = [] },
            new Analysis { UserId = userId, Name = "Selected", Zones = [new AnalysisZone { ZoneSlug = "recoleta", Name = "Recoleta" }] }
        };
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CompareAnalyses_WhenAnAnalysisHasMoreThanTenZones_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var analyses = new[]
        {
            new Analysis
            {
                UserId = userId,
                Name = "Too many zones",
                Zones = Enumerable.Range(1, 11)
                    .Select(index => new AnalysisZone { ZoneSlug = $"zone-{index}", Name = $"Zone {index}" })
                    .ToList()
            },
            new Analysis { UserId = userId, Name = "Other", Zones = [new AnalysisZone { ZoneSlug = "other", Name = "Other" }] }
        };
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task CompareAnalyses_WhenPersonalResultsAreSaved_UsesTheSnapshotWithoutRecalculating()
    {
        var userId = Guid.NewGuid();
        var savedResult = JsonSerializer.Serialize(
            new ScoresResponse("auto", [new ScoredZoneResponse("recoleta", "Recoleta", 82, [], [], [], null)], []),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Analysis {index}",
            ResultJson = savedResult,
            Zones = [new AnalysisZone { ZoneSlug = "recoleta", Name = "Recoleta", Order = 1 }]
        }).ToArray();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var zoneProfiles = new Mock<IZoneProfilesRepository>();
        var routing = new Mock<IRoutingClient>();

        var result = await new CompareAnalyses(repository.Object, zoneProfiles.Object, routing.Object)
            .ExecuteAsync(userId, new CompareAnalysesRequest
            {
                AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
            });

        result.Analyses.Should().HaveCount(2);
        result.Analyses.Should().AllSatisfy(analysis =>
            analysis.Zones.Should().ContainSingle().Which.Score.Should().Be(82));
        zoneProfiles.Verify(item => item.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
        routing.Verify(item => item.GetTravelTimeAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompareAnalyses_WhenGroupResultsAreSaved_UsesTheStoredRankingSnapshot()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Group analysis {index}",
            Zones = [new AnalysisZone { ZoneSlug = $"zone-{index}", Name = $"Zone {index}", Order = 1 }],
            GroupScoresJson = JsonSerializer.Serialize(
                new GroupRankingResponse(
                    [new RankedZoneResponse($"zone-{index}", $"Zone {index}", 80 - index, [])],
                    [],
                    []),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }).ToArray();
        var analysisRepository = new Mock<IAnalysisRepository>();
        analysisRepository.Setup(repository => repository.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analyses);
        var zoneProfilesRepository = new Mock<IZoneProfilesRepository>();
        var routingClient = new Mock<IRoutingClient>();
        var useCase = new CompareAnalyses(analysisRepository.Object, zoneProfilesRepository.Object, routingClient.Object);

        var response = await useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        response.Analyses.Should().HaveCount(2);
        response.Analyses.Should().AllSatisfy(analysis =>
        {
            analysis.TravelMode.Should().Be("grupo");
            analysis.Zones.Should().ContainSingle();
        });
        zoneProfilesRepository.Verify(repository => repository.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
        routingClient.Verify(client => client.GetTravelTimeAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<TravelMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompareAnalyses_WhenEverySavedResultHasNoScores_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var savedResult = JsonSerializer.Serialize(
            new ScoresResponse("auto", [new ScoredZoneResponse("recoleta", "Recoleta", null, [], [], [], null)], []),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Analysis {index}",
            ResultJson = savedResult,
            Zones = [new AnalysisZone { ZoneSlug = "recoleta", Name = "Recoleta", Order = 1 }]
        }).ToArray();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("No hay datos suficientes para puntuar las zonas de los análisis elegidos.");
    }

    [Fact]
    public async Task CompareAnalyses_WhenLegacyPersonalZoneIsUnavailable_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Legacy analysis {index}",
            Zones = [new AnalysisZone { ZoneSlug = "removed-zone", Name = "Removed zone", Order = 1 }],
            Context = new Context { UserId = userId, Criteria = [], Places = [], Mobility = [] }
        }).ToArray();
        var analysisRepository = new Mock<IAnalysisRepository>();
        analysisRepository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var zoneProfilesRepository = new Mock<IZoneProfilesRepository>();
        zoneProfilesRepository.Setup(item => item.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ZoneProfile[] { new() { Slug = "other-zone", Name = "Other zone" } });
        var useCase = new CompareAnalyses(
            analysisRepository.Object,
            zoneProfilesRepository.Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("No hay datos para calcular estas zonas: Removed zone.");
    }

    [Fact]
    public async Task CompareAnalyses_WhenGroupSnapshotIsMalformed_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Group analysis {index}",
            Zones = [new AnalysisZone { ZoneSlug = $"zone-{index}", Name = $"Zone {index}", Order = 1 }],
            GroupScoresJson = "{invalid-json"
        }).ToArray();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("El análisis de grupo '*' no tiene un resultado válido guardado.");
    }

    [Fact]
    public async Task CompareAnalyses_WhenGroupSnapshotDoesNotContainEverySavedZone_ThrowsBadRequestException()
    {
        var userId = Guid.NewGuid();
        var analyses = Enumerable.Range(1, 2).Select(index => new Analysis
        {
            UserId = userId,
            Name = $"Group analysis {index}",
            Zones = [new AnalysisZone { ZoneSlug = $"zone-{index}", Name = $"Zone {index}", Order = 1 }],
            GroupScoresJson = JsonSerializer.Serialize(
                new GroupRankingResponse([new RankedZoneResponse("other-zone", "Other zone", 75, [])], [], []),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }).ToArray();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(analyses);
        var useCase = new CompareAnalyses(
            repository.Object,
            new Mock<IZoneProfilesRepository>().Object,
            new Mock<IRoutingClient>().Object);

        Func<Task> action = () => useCase.ExecuteAsync(userId, new CompareAnalysesRequest
        {
            AnalysisIds = analyses.Select(analysis => analysis.Id).ToList()
        });

        await action.Should().ThrowAsync<BadRequestException>()
            .WithMessage("El análisis de grupo '*' no tiene puntajes guardados para todas sus zonas. Volvé a calcular y guardar ese ranking.");
    }
}
