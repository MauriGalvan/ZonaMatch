using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Contribution;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Community;

namespace ZonaMatch.Tests.UseCases.Contribution
{
    public class CreateContributionCorrectionTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeContributionRepository _contributions = new();
        private readonly CreateContributionCorrection _useCase;
        private readonly AuthenticatedUserContext _author = new(Guid.NewGuid(), "ana.perez@example.com");

        public CreateContributionCorrectionTests()
        {
            _useCase = new CreateContributionCorrection(_contributions, new FakeZoneRepository(), new FixedTimeProvider(Now));
        }

        private static CreateContributionCorrectionRequest Correction(
            CorrectionReason reason, string? proposedName = null, string? proposedCategory = null,
            double? latitude = null, double? longitude = null, string? comment = null) =>
            new("n123", "Kiosco Pepe", reason, proposedName, proposedCategory, latitude, longitude, comment);

        [Fact]
        public async Task CreateCorrection_WhenValid_SavesOnlyTheSelectedValue()
        {
            await _useCase.ExecuteAsync(Zone, _author,
                Correction(CorrectionReason.Name, proposedName: "Kiosco Pepito", proposedCategory: "salud", latitude: -34.6, longitude: -58.5));

            var savedContribution = Assert.Single(_contributions.Contributions);
            Assert.Equal(ContributionType.Correction, savedContribution.Type);
            Assert.Equal(ContributionStatus.Pending, savedContribution.Status);
            Assert.Equal("n123", savedContribution.PointOfInterestId);
            Assert.Equal("Kiosco Pepito", savedContribution.Name);
            Assert.Null(savedContribution.Category);
            Assert.Null(savedContribution.Location);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsLocation_SavesTheProposedPoint()
        {
            await _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Location, latitude: -34.6, longitude: -58.5));

            var location = Assert.Single(_contributions.Contributions).Location!;
            Assert.Equal(-34.6, location.Y);
            Assert.Equal(-58.5, location.X);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsCategory_SavesTheNormalizedCategory()
        {
            await _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Category, proposedCategory: " Salud "));

            var contribution = Assert.Single(_contributions.Contributions);
            Assert.Equal(CorrectionReason.Category, contribution.Reason);
            Assert.Equal("salud", contribution.Category);
            Assert.Null(contribution.Name);
            Assert.Null(contribution.Location);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsOtherWithComment_SavesTheNormalizedComment()
        {
            await _useCase.ExecuteAsync(Zone, _author,
                Correction(CorrectionReason.Other, comment: "  Abre solo por la tarde.  "));

            var contribution = Assert.Single(_contributions.Contributions);
            Assert.Equal(CorrectionReason.Other, contribution.Reason);
            Assert.Equal("Abre solo por la tarde.", contribution.Comment);
            Assert.Null(contribution.Name);
            Assert.Null(contribution.Category);
            Assert.Null(contribution.Location);
        }

        [Theory]
        [InlineData(CorrectionReason.Name)]
        [InlineData(CorrectionReason.Category)]
        [InlineData(CorrectionReason.Location)]
        [InlineData(CorrectionReason.Other)]
        public async Task CreateCorrection_WhenRequiredValueIsMissing_ThrowsBadRequestException(CorrectionReason reason)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Correction(reason)));
            Assert.Empty(_contributions.Contributions);
        }

        [Fact]
        public async Task CreateCorrection_WhenLocationIsOutsideZone_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Location, latitude: 10, longitude: -58.5)));
            Assert.Empty(_contributions.Contributions);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsClosed_DoesNotRequireAdditionalValues()
        {
            await _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Closed));

            Assert.Equal(CorrectionReason.Closed, Assert.Single(_contributions.Contributions).Reason);
        }

        [Fact]
        public async Task CreateCorrection_WhenZoneDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync("no-existe", _author, Correction(CorrectionReason.Closed)));
            Assert.Empty(_contributions.Contributions);
        }
    }
}
