using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Application.UseCases.Profile;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Profile;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis
{
    public class SavedAnalysesTests
    {
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid UserId = Guid.NewGuid();

        private readonly FakeProfileRepository _profiles = new();
        private readonly FakeAnalysisRepository _analyses = new();
        private readonly FakeCriterionRepository _criteria = new();
        private readonly FakeZoneRepository _zones = new();
        private readonly FixedTimeProvider _clock = new(CurrentTime);
        private readonly FakeResultCalculator _results = new();

        private CreateAnalysis Create => new(_analyses, _profiles, _zones, _clock, _results);
        private CreateAnalysisVersion CreateVersion => new(_analyses, _criteria, _zones, _clock, _results);
        private RecalculateAnalysis Recalculate => new(_analyses, _results, _clock);

        private async Task WithProfile(string criterion = "seguridad", string priority = "alta") =>
            await new SaveMyProfile(_profiles, _criteria, _clock)
                .ExecuteAsync(UserId, TestRequests.AnalysisCriteria(new() { [criterion] = priority }));

        private static CreateAnalysisRequest AnalysisRequest(string name = "Cerca del trabajo", params AnalysisZoneRequest[] zones) =>
            new() { Name = name, Zones = zones.Length > 0 ? [.. zones] : [TestRequests.AnalysisZone(isInitial: true)] };

        [Fact]
        public async Task Create_WhenSaved_KeepsTheResultAndWhenItWasCalculated()
        {
            await WithProfile();
            _results.Score = 81;

            var dto = await Create.ExecuteAsync(UserId, AnalysisRequest());

            Assert.Equal(81, Assert.Single(dto.Result!.Zones).Score);
            Assert.Equal(CurrentTime, dto.CalculatedAt);
        }

        [Fact]
        public async Task Create_WhenNoZonesAreProvided_ThrowsBadRequestWithoutSaving()
        {
            await WithProfile();
            var request = new CreateAnalysisRequest { Name = "No zones", Zones = [] };

            await Assert.ThrowsAsync<BadRequestException>(() => Create.ExecuteAsync(UserId, request));

            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Create_WhenMoreThanOneZoneIsInitial_ThrowsBadRequestWithoutSaving()
        {
            await WithProfile();
            var request = AnalysisRequest("Multiple initial zones",
                TestRequests.AnalysisZone(isInitial: true),
                TestRequests.AnalysisZone("another-zone", isInitial: true));

            await Assert.ThrowsAsync<BadRequestException>(() => Create.ExecuteAsync(UserId, request));

            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Create_WhenResultCalculatorReturnsNull_SavesAnalysisWithoutResultSnapshot()
        {
            await WithProfile();
            _results.ReturnsNull = true;

            var response = await Create.ExecuteAsync(UserId, AnalysisRequest());

            var savedAnalysis = Assert.Single(_analyses.Analyses);
            Assert.Null(response.Result);
            Assert.Null(response.CalculatedAt);
            Assert.Null(savedAnalysis.ResultJson);
            Assert.Null(savedAnalysis.ResultGeneratedAt);
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisIsSaved_CreatesAVersionWithTodaysResultAndKeepsTheOriginal()
        {
            await WithProfile();
            _results.Score = 81;
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            _results.Score = 74;

            var recalculated = await Recalculate.ExecuteAsync(original.Id, UserId);

            Assert.Equal(original.Id, recalculated.OriginAnalysisId);
            Assert.Equal("Cerca del trabajo · datos al 14/10", recalculated.Name);
            Assert.Equal(74, Assert.Single(recalculated.Result!.Zones).Score);
            Assert.Equal(original.Criteria, recalculated.Criteria);
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);
            Assert.Equal(81, AnalysisResultCalculator.Deserialize(saved.ResultJson)!.Zones.Single().Score);
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Recalculate.ExecuteAsync(Guid.NewGuid(), UserId));
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisIsAGroupAnalysis_ThrowsBadRequestWithoutSaving()
        {
            var groupAnalysis = new Analysis
            {
                UserId = UserId,
                Name = "Group ranking",
                GroupScoresJson = "{}",
                Context = new Context { UserId = UserId, Type = ContextType.Analysis },
                Zones = [new AnalysisZone { ZoneSlug = FakeZoneRepository.ZoneSlug, Name = "Villa Luro", Order = 1 }]
            };
            _analyses.Add(groupAnalysis);

            await Assert.ThrowsAsync<BadRequestException>(() => Recalculate.ExecuteAsync(groupAnalysis.Id, UserId));

            Assert.Single(_analyses.Analyses);
            Assert.Equal(0, _analyses.SaveCount);
        }

        [Fact]
        public async Task Recalculate_WhenSourceIsAVersion_AttachesNewVersionToTheOriginalAnalysis()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Cerca del trabajo"));
            var sourceVersion = await CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria()
            });

            var recalculated = await Recalculate.ExecuteAsync(sourceVersion.Id, UserId);

            Assert.Equal(original.Id, recalculated.OriginAnalysisId);
            Assert.Equal("Cerca del trabajo · datos al 14/10", recalculated.Name);
        }

        [Fact]
        public async Task Recalculate_WhenResultCalculatorReturnsNull_SavesVersionWithoutResultSnapshot()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            _results.ReturnsNull = true;

            var response = await Recalculate.ExecuteAsync(original.Id, UserId);

            var savedVersion = Assert.Single(_analyses.Analyses.Where(analysis => analysis.OriginalAnalysisId == original.Id));
            Assert.Null(response.Result);
            Assert.Null(response.CalculatedAt);
            Assert.Null(savedVersion.ResultJson);
            Assert.Null(savedVersion.ResultGeneratedAt);
        }

        private static AnalysisZoneRequest ZoneWithOrigin(double latitude) =>
            new()
            {
                Slug = FakeZoneRepository.ZoneSlug, Name = "Villa Luro, CABA", IsInitial = true,
                Origin = new AnalysisZoneOriginRequest { Latitude = latitude, Longitude = -58.5 }
            };

        [Fact]
        public async Task Create_WithAStartingPoint_SavesItAndCalculatesFromIt()
        {
            await WithProfile();

            var dto = await Create.ExecuteAsync(UserId, AnalysisRequest("Con punto", ZoneWithOrigin(-34.63)));

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(dto.Zones).Origin);
            var calculated = Assert.Single(Assert.Single(_results.Calculations).Zones);
            Assert.Equal((-34.63, -58.5), (calculated.OriginLatitude, calculated.OriginLongitude));
        }

        [Fact]
        public async Task Create_WithAStartingPointOutsideTheZone_ThrowsBadRequestException()
        {
            await WithProfile();

            await Assert.ThrowsAsync<BadRequestException>(() => Create.ExecuteAsync(UserId, AnalysisRequest("Afuera", ZoneWithOrigin(10))));
            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Recalculate_KeepsTheStartingPointsOfTheZones()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Con punto", ZoneWithOrigin(-34.63)));

            var recalculated = await Recalculate.ExecuteAsync(original.Id, UserId);

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(recalculated.Zones).Origin);
        }

        private sealed class FakeResultCalculator : IAnalysisResultCalculator
        {
            public int? Score { get; set; } = 50;
            public bool ReturnsNull { get; set; }

            // Analyses received, to check what the calculation used
            public List<Analysis> Calculations { get; } = [];

            public Task<ScoresResponse?> CalculateAsync(Analysis analysis, CancellationToken cancellationToken = default)
            {
                Calculations.Add(analysis);
                if (ReturnsNull)
                    return Task.FromResult<ScoresResponse?>(null);

                return Task.FromResult<ScoresResponse?>(new ScoresResponse(
                    "transporte-publico",
                    analysis.Zones.Select(zone => new ScoredZoneResponse(zone.ZoneSlug, zone.Name, Score, [], [], [], null)).ToList(),
                    []));
            }
        }

        [Fact]
        public async Task Create_WhenProfileDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Create.ExecuteAsync(UserId, AnalysisRequest()));
            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Create_StoresAProfileSnapshotThatDoesNotChangeWhenTheProfileIsEdited()
        {
            await WithProfile("seguridad", "alta");

            var dto = await Create.ExecuteAsync(UserId, AnalysisRequest());

            var analysis = Assert.Single(_analyses.Analyses);
            var profile = Assert.Single(_profiles.Profiles);
            Assert.NotSame(profile, analysis.Context);
            Assert.Equal(ContextType.Analysis, analysis.Context.Type);
            Assert.Null(analysis.OriginalAnalysisId);
            Assert.Equal("alta", dto.Criteria["seguridad"]);
            Assert.True(Assert.Single(dto.Zones).IsInitial);

            await WithProfile("salud", "baja");

            Assert.Equal("seguridad", Assert.Single(analysis.Context.Criteria).CriterionId);
        }

        [Fact]
        public async Task Create_WhenZoneDoesNotExist_ThrowsNotFound()
        {
            await WithProfile();

            await Assert.ThrowsAsync<NotFoundException>(
                () => Create.ExecuteAsync(UserId, AnalysisRequest(zones: TestRequests.AnalysisZone("no-existe"))));
            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Create_WithDuplicateZones_ThrowsBadRequest()
        {
            await WithProfile();

            var error = await Assert.ThrowsAsync<BadRequestException>(
                () => Create.ExecuteAsync(UserId, AnalysisRequest(zones: [TestRequests.AnalysisZone(), TestRequests.AnalysisZone()])));
            Assert.Contains("repetidas", error.Message);
        }

        [Fact]
        public async Task CreateVersion_UsesAdjustedCriteriaAndAttachesToTheOriginal()
        {
            await WithProfile("seguridad", "alta");
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Cerca del trabajo"));

            var version = await CreateVersion.ExecuteAsync(original.Id, UserId, new CreateAnalysisVersionRequest
            {
                Criterion = TestRequests.AnalysisCriteria(new() { ["salud"] = "alta" }),
            });

            Assert.Equal(original.Id, version.OriginAnalysisId);
            Assert.Equal("Cerca del trabajo (2)", version.Name);
            Assert.Equal("alta", version.Criteria["salud"]);
            // When the request has no zones, compare the same zones as the original.
            Assert.Equal(FakeZoneRepository.ZoneSlug, Assert.Single(version.Zones).Slug);
            // The original remains unchanged.
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);
            Assert.Equal("seguridad", Assert.Single(saved.Context.Criteria).CriterionId);
        }

        [Fact]
        public async Task CreateVersion_WhenResultCalculatorReturnsNull_SavesVersionWithoutResultSnapshot()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            _results.ReturnsNull = true;

            var version = await CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria()
            });

            var savedVersion = Assert.Single(_analyses.Analyses.Where(analysis => analysis.OriginalAnalysisId == original.Id));
            Assert.Null(version.Result);
            Assert.Null(version.CalculatedAt);
            Assert.Null(savedVersion.ResultJson);
            Assert.Null(savedVersion.ResultGeneratedAt);
        }

        [Fact]
        public async Task CreateVersion_WhenExplicitZonesAreProvided_UsesThemWithoutChangingTheOriginal()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Original"));
            var requestedZone = TestRequests.AnalysisZone(isInitial: false);

            var version = await CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Name = "Version with selected zones",
                Criterion = TestRequests.AnalysisCriteria(),
                Zones = [requestedZone]
            });

            var versionZone = Assert.Single(version.Zones);
            Assert.Equal(requestedZone.Slug, versionZone.Slug);
            Assert.Equal(requestedZone.Name, versionZone.Name);
            Assert.False(versionZone.IsInitial);
            Assert.Equal(original.Id, version.OriginAnalysisId);
            Assert.True(Assert.Single(_analyses.Analyses.Single(analysis => analysis.Id == original.Id).Zones).IsInitial);
        }

        [Fact]
        public async Task CreateVersion_WhenExplicitZoneDoesNotExist_ThrowsNotFoundWithoutSavingVersion()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            var analysisCountBeforeVersion = _analyses.Analyses.Count;
            var saveCountBeforeVersion = _analyses.SaveCount;

            await Assert.ThrowsAsync<NotFoundException>(() => CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria(),
                Zones = [TestRequests.AnalysisZone("no-existe")]
            }));

            Assert.Equal(analysisCountBeforeVersion, _analyses.Analyses.Count);
            Assert.Equal(saveCountBeforeVersion, _analyses.SaveCount);
        }

        [Fact]
        public async Task CreateVersion_WhenExplicitZonesContainDuplicates_ThrowsBadRequestWithoutSavingVersion()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            var analysisCountBeforeVersion = _analyses.Analyses.Count;
            var saveCountBeforeVersion = _analyses.SaveCount;

            await Assert.ThrowsAsync<BadRequestException>(() => CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria(),
                Zones = [TestRequests.AnalysisZone(), TestRequests.AnalysisZone()]
            }));

            Assert.Equal(analysisCountBeforeVersion, _analyses.Analyses.Count);
            Assert.Equal(saveCountBeforeVersion, _analyses.SaveCount);
        }

        [Fact]
        public async Task CreateVersion_WhenExplicitZonesHaveMultipleInitialZones_ThrowsBadRequestWithoutSavingVersion()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            var analysisCountBeforeVersion = _analyses.Analyses.Count;
            var saveCountBeforeVersion = _analyses.SaveCount;

            await Assert.ThrowsAsync<BadRequestException>(() => CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria(),
                Zones =
                [
                    TestRequests.AnalysisZone(isInitial: true),
                    TestRequests.AnalysisZone("another-zone", isInitial: true)
                ]
            }));

            Assert.Equal(analysisCountBeforeVersion, _analyses.Analyses.Count);
            Assert.Equal(saveCountBeforeVersion, _analyses.SaveCount);
        }

        [Fact]
        public async Task CreateVersion_WhenExplicitZoneOriginIsOutsideTheZone_ThrowsBadRequestWithoutSavingVersion()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            var analysisCountBeforeVersion = _analyses.Analyses.Count;
            var saveCountBeforeVersion = _analyses.SaveCount;

            await Assert.ThrowsAsync<BadRequestException>(() => CreateVersion.ExecuteAsync(original.Id, UserId, new()
            {
                Criterion = TestRequests.AnalysisCriteria(),
                Zones = [ZoneWithOrigin(10)]
            }));

            Assert.Equal(analysisCountBeforeVersion, _analyses.Analyses.Count);
            Assert.Equal(saveCountBeforeVersion, _analyses.SaveCount);
        }

        [Fact]
        public async Task CreateVersion_FromAnotherVersion_StillAttachesToTheOriginal()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Facu"));
            var first = await CreateVersion.ExecuteAsync(original.Id, UserId, new() { Criterion = TestRequests.AnalysisCriteria() });

            var second = await CreateVersion.ExecuteAsync(first.Id, UserId, new() { Criterion = TestRequests.AnalysisCriteria() });

            Assert.Equal(original.Id, second.OriginAnalysisId);
            Assert.Equal("Facu (3)", second.Name);
        }

        [Fact]
        public async Task CreateVersion_WhenAnalysisBelongsToAnotherUser_ThrowsNotFound()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());

            await Assert.ThrowsAsync<NotFoundException>(
                () => CreateVersion.ExecuteAsync(original.Id, Guid.NewGuid(), new() { Criterion = TestRequests.AnalysisCriteria() }));
        }

        [Fact]
        public async Task List_ReturnsOriginalAnalysesWithTheirVersions()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("A"));
            await Create.ExecuteAsync(UserId, AnalysisRequest("B"));
            await CreateVersion.ExecuteAsync(original.Id, UserId, new() { Criterion = TestRequests.AnalysisCriteria() });

            var list = await new ListMyAnalyses(_analyses).ExecuteAsync(UserId);

            Assert.Equal(2, list.Count);
            var a = list.Single(item => item.Name == "A");
            Assert.Equal("A (2)", Assert.Single(a.Versions).Name);
            Assert.Empty(list.Single(item => item.Name == "B").Versions);
        }

        [Fact]
        public async Task List_WhenUserHasNoSavedAnalyses_ReturnsAnEmptyList()
        {
            var analyses = await new ListMyAnalyses(_analyses).ExecuteAsync(UserId);

            Assert.Empty(analyses);
        }

        [Fact]
        public async Task Rename_ChangesOnlyTheName()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Viejo"));

            var dto = await new RenameAnalysis(_analyses, _clock)
                .ExecuteAsync(original.Id, UserId, new RenameAnalysisRequest { Name = "  Nuevo " });

            Assert.Equal("Nuevo", dto.Name);
            Assert.Equal(original.Criteria, dto.Criteria);
        }

        [Fact]
        public async Task Rename_WhenAnalysisDoesNotExist_ThrowsNotFoundWithoutSaving()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new RenameAnalysis(_analyses, _clock)
                .ExecuteAsync(Guid.NewGuid(), UserId, new RenameAnalysisRequest { Name = "New name" }));

            Assert.Empty(_analyses.Analyses);
            Assert.Equal(0, _analyses.SaveCount);
        }

        [Fact]
        public async Task Favorite_IsSavedWithoutChangingTheUpdateTime()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Favorito"));
            var useCase = new SetAnalysisFavorite(_analyses);

            await useCase.ExecuteAsync(original.Id, UserId, true);
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);

            Assert.True(saved.IsFavorite);
            Assert.Equal(original.UpdatedAt, saved.UpdatedAt);
            await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(original.Id, Guid.NewGuid(), true));
        }

        [Fact]
        public async Task Favorite_WhenValueIsAlreadySet_DoesNotSaveAgain()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Favorito"));
            var useCase = new SetAnalysisFavorite(_analyses);
            await useCase.ExecuteAsync(original.Id, UserId, true);
            var saveCountAfterFirstChange = _analyses.SaveCount;

            await useCase.ExecuteAsync(original.Id, UserId, true);

            Assert.Equal(saveCountAfterFirstChange, _analyses.SaveCount);
        }

        [Fact]
        public void BuildVersionName_TrimsToStayWithinMaximumLength()
        {
            var name = CreateAnalysisVersion.BuildVersionName(new string('a', 50), 12);

            Assert.Equal(50, name.Length);
            Assert.EndsWith(" (12)", name);
        }
    }
}
