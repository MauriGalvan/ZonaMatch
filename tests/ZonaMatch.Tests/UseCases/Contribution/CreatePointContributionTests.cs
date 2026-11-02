using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Contribution;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contribution
{
    public class CreatePointContributionTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeContributionRepository _contributions = new();
        private readonly CreatePointContribution _useCase;
        private readonly AuthenticatedUserContext _author = new(Guid.NewGuid(), "ana.perez@example.com");

        public CreatePointContributionTests()
        {
            _useCase = new CreatePointContribution(_contributions, new FakeZoneRepository(), new FixedTimeProvider(Now));
        }

        private static CreatePointContributionRequest Point(string category = "salud", double latitude = -34.63) =>
            new(category, " Farmacia Nueva ", "Lun a vie 9 a 20", "  ", latitude, -58.50, LivesOrWorksInZone: true);

        [Fact]
        public async Task CreatePoint_WhenValid_SavesPendingContributionWithNormalizedValues()
        {
            var response = await _useCase.ExecuteAsync(Zone, _author, Point(category: " Salud "));

            var savedContribution = Assert.Single(_contributions.Contributions);
            Assert.Equal(ContributionType.NewPoint, savedContribution.Type);
            Assert.Equal(ContributionStatus.Pending, savedContribution.Status);
            Assert.Equal("salud", savedContribution.Category);
            Assert.Equal("Farmacia Nueva", savedContribution.Name);
            Assert.Null(savedContribution.Address);
            Assert.Equal(-34.63, savedContribution.Location!.Y);
            Assert.Equal(4326, savedContribution.Location.SRID);
            Assert.Equal(Now, savedContribution.CreatedAt);
            Assert.Equal("AP", response.AuthorInitials);
            Assert.True(response.IsMine);
        }

        [Fact]
        public async Task CreatePoint_WhenOutsideZone_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Point(latitude: 10)));
            Assert.Empty(_contributions.Contributions);
        }

        [Fact]
        public async Task CreatePoint_WhenZoneDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe", _author, Point()));
            Assert.Empty(_contributions.Contributions);
        }

        [Fact]
        public async Task CreatePoint_WhenCategoryIsUnknown_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Point(category: "boliches")));
            Assert.Empty(_contributions.Contributions);
        }
    }
}
