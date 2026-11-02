using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Profile;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Profile
{
    public class SaveMyProfileTests
    {
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid UserId = Guid.NewGuid();

        private readonly FakeProfileRepository _profiles = new();
        private readonly SaveMyProfile _useCase;

        public SaveMyProfileTests()
        {
            _useCase = new SaveMyProfile(_profiles, new FakeCriterionRepository(), new FixedTimeProvider(CurrentTime));
        }

        [Fact]
        public async Task Save_WhenProfileDoesNotExist_CreatesItWithItsRows()
        {
            var profileResponse = await _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(
                new() { ["seguridad"] = "alta", ["salud"] = "baja" },
                peopleCount: 2,
                ageRanges: ["26–35 años"],
                places: [new AnalysisPlaceRequest { Name = " Facultad ", Latitude = -34.6, Longitude = -58.4, Type = "Estudio", MaximumTravelTime = "45 min" }],
                rentLimit: "800000"));

            var profile = Assert.Single(_profiles.Profiles);
            Assert.Equal(ContextType.Profile, profile.Type);
            Assert.Equal(UserId, profile.UserId);
            Assert.Equal(CurrentTime, profile.CreatedAt);
            Assert.Equal(2, profile.People.Count);
            Assert.Equal(["26–35 años", null], profile.People.OrderBy(person => person.Order).Select(person => person.AgeRange));
            Assert.Equal("Facultad", Assert.Single(profile.Places).Name);
            Assert.Equal("800000", profile.MaximumRent);

            Assert.Equal("alta", profileResponse.Criteria["seguridad"]);
            Assert.Equal("baja", profileResponse.Criteria["salud"]);
            Assert.Equal(1, _profiles.SaveCount);
        }

        [Fact]
        public async Task Save_WhenProfileExists_ReplacesItWithoutCreatingAnother()
        {
            await _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(new() { ["seguridad"] = "alta" }, peopleCount: 3));

            await _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(new() { ["salud"] = "media" }, peopleCount: 1));

            var profile = Assert.Single(_profiles.Profiles);
            Assert.Single(profile.People);
            var criterion = Assert.Single(profile.Criteria);
            Assert.Equal("salud", criterion.CriterionId);
        }

        [Theory]
        [InlineData("turismo", "alta", "Criterio desconocido")]
        [InlineData("transporte", "alta", "Criterio no disponible")]
        [InlineData("seguridad", "urgentisima", "Prioridad inválida")]
        [InlineData("seguridad", null, "Prioridad inválida")]
        public async Task Save_WithInvalidCriterion_ThrowsAndDoesNotSave(string criterion, string? priority, string message)
        {
            var error = await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(new() { [criterion] = priority })));

            Assert.Contains(message, error.Message);
            Assert.Empty(_profiles.Profiles);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        public async Task Save_WhenPeopleCountIsOutOfRange_Throws(int peopleCount)
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(peopleCount: peopleCount)));

            Assert.Empty(_profiles.Profiles);
        }

        [Fact]
        public async Task Save_WhenThereAreMoreThanFifteenPlaces_Throws()
        {
            var places = Enumerable.Range(1, 16).Select(index => new AnalysisPlaceRequest { Name = $"Lugar {index}" }).ToList();

            var error = await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(places: places)));

            Assert.Equal("Se pueden medir hasta 15 lugares.", error.Message);
            Assert.Empty(_profiles.Profiles);
        }

        [Fact]
        public async Task Save_WhenPlaceHasNoName_ThrowsAndLeavesExistingProfileUntouched()
        {
            await _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(new() { ["seguridad"] = "alta" }));

            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(UserId, TestRequests.AnalysisCriteria(
                new() { ["salud"] = "media" },
                places: [new AnalysisPlaceRequest { Name = "  " }])));

            // No partial updates: the profile keeps its previous criterion.
            Assert.Equal("seguridad", Assert.Single(Assert.Single(_profiles.Profiles).Criteria).CriterionId);
        }

        [Fact]
        public async Task GetMyProfile_WhenProfileDoesNotExist_ReturnsNull()
        {
            Assert.Null(await new GetMyProfile(_profiles).ExecuteAsync(UserId));
        }
    }
}
