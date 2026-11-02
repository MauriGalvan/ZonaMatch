using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Application.UseCases.Profile;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Profile;

namespace ZonaMatch.Tests.UseCases.SavedAnalyses
{
    public class SavedAnalysesTests
    {
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid UserId = Guid.NewGuid();

        private readonly FakeProfileRepository _profiles = new();
        private readonly FakeAnalysisRepository _analyses = new();
        private readonly FakeCriterionRepository _criteria = new();
        private readonly FakeZonaRepository _zonas = new();
        private readonly FixedTimeProvider _clock = new(CurrentTime);
        private readonly FakeResultCalculator _results = new();

        private CreateAnalysis Create => new(_analyses, _profiles, _zonas, _clock, _results);
        private CreateAnalysisVersion CreateVersion => new(_analyses, _criteria, _zonas, _clock, _results);
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

            Assert.Equal(81, Assert.Single(dto.Result!.Zonas).Puntaje);
            Assert.Equal(CurrentTime, dto.CalculatedAt);
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
            Assert.Equal(74, Assert.Single(recalculated.Result!.Zonas).Puntaje);
            Assert.Equal(original.Criteria, recalculated.Criteria);
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);
            Assert.Equal(81, AnalysisResultCalculator.Deserialize(saved.ResultadoJson)!.Zonas.Single().Puntaje);
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Recalculate.ExecuteAsync(Guid.NewGuid(), UserId));
        }

        private static AnalysisZoneRequest ZonaConOrigen(double latitude) =>
            new()
            {
                Slug = FakeZonaRepository.Zona, Name = "Villa Luro, CABA", IsInitial = true,
                Origin = new AnalysisZoneOriginRequest { Latitude = latitude, Longitude = -58.5 }
            };

        [Fact]
        public async Task Create_WithAStartingPoint_SavesItAndCalculatesFromIt()
        {
            await WithProfile();

            var dto = await Create.ExecuteAsync(UserId, AnalysisRequest("Con punto", ZonaConOrigen(-34.63)));

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(dto.Zones).Origin);
            var calculated = Assert.Single(Assert.Single(_results.Calculations).Zonas);
            Assert.Equal((-34.63, -58.5), (calculated.OrigenLatitud, calculated.OrigenLongitud));
        }

        [Fact]
        public async Task Create_WithAStartingPointOutsideTheZone_ThrowsBadRequestException()
        {
            await WithProfile();

            await Assert.ThrowsAsync<BadRequestException>(() => Create.ExecuteAsync(UserId, AnalysisRequest("Afuera", ZonaConOrigen(10))));
            Assert.Empty(_analyses.Analyses);
        }

        [Fact]
        public async Task Recalculate_KeepsTheStartingPointsOfTheZones()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Con punto", ZonaConOrigen(-34.63)));

            var recalculated = await Recalculate.ExecuteAsync(original.Id, UserId);

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(recalculated.Zones).Origin);
        }

        private sealed class FakeResultCalculator : IAnalysisResultCalculator
        {
            public int? Score { get; set; } = 50;

            // Analyses received, to check what the calculation used
            public List<Analisis> Calculations { get; } = [];

            public Task<ScoresResponse?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default)
            {
                Calculations.Add(analysis);
                return Task.FromResult<ScoresResponse?>(new ScoresResponse(
                    "transporte-publico",
                    analysis.Zonas.Select(zone => new ScoredZoneResponse(zone.ZonaSlug, zone.Nombre, Score, [], [], [], null)).ToList(),
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
            Assert.NotSame(profile, analysis.Contexto);
            Assert.Equal(TipoContexto.Analisis, analysis.Contexto.Tipo);
            Assert.Null(analysis.AnalisisOrigenId);
            Assert.Equal("alta", dto.Criteria["seguridad"]);
            Assert.True(Assert.Single(dto.Zones).IsInitial);

            await WithProfile("salud", "baja");

            Assert.Equal("seguridad", Assert.Single(analysis.Contexto.Criterios).CriterioId);
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
            Assert.Equal(FakeZonaRepository.Zona, Assert.Single(version.Zones).Slug);
            // The original remains unchanged.
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);
            Assert.Equal("seguridad", Assert.Single(saved.Contexto.Criterios).CriterioId);
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
        public async Task Delete_OriginalAnalysis_AlsoDeletesItsVersions()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest());
            await CreateVersion.ExecuteAsync(original.Id, UserId, new() { Criterion = TestRequests.AnalysisCriteria() });
            var other = await Create.ExecuteAsync(UserId, AnalysisRequest("Otro"));

            await new DeleteAnalysis(_analyses).ExecuteAsync(original.Id, UserId);

            Assert.Equal(other.Id, Assert.Single(_analyses.Analyses).Id);
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
        public async Task Favorite_IsSavedWithoutChangingTheUpdateTime()
        {
            await WithProfile();
            var original = await Create.ExecuteAsync(UserId, AnalysisRequest("Favorito"));
            var useCase = new SetAnalysisFavorite(_analyses);

            await useCase.ExecuteAsync(original.Id, UserId, true);
            var saved = _analyses.Analyses.Single(analysis => analysis.Id == original.Id);

            Assert.True(saved.EsFavorito);
            Assert.Equal(original.UpdatedAt, saved.FechaActualizacion);
            await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(original.Id, Guid.NewGuid(), true));
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
