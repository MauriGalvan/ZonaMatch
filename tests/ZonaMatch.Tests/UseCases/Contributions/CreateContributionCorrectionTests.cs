using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class CreateContributionCorrectionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _contributions = new();
        private readonly CreateContributionCorrection _useCase;
        private readonly AuthenticatedUserContext _author = new(Guid.NewGuid(), "ana.perez@example.com");

        public CreateContributionCorrectionTests()
        {
            _useCase = new CreateContributionCorrection(_contributions, new FakeZonaRepository(), new FixedTimeProvider(Now));
        }

        private static CreateContributionCorrectionRequest Correction(
            CorrectionReason reason, string? proposedName = null, string? proposedCategory = null,
            double? latitude = null, double? longitude = null, string? comment = null) =>
            new("n123", "Kiosco Pepe", reason, proposedName, proposedCategory, latitude, longitude, comment);

        [Fact]
        public async Task CreateCorrection_WhenValid_SavesOnlyTheSelectedValue()
        {
            await _useCase.ExecuteAsync(Zone, _author,
                Correction(CorrectionReason.Nombre, proposedName: "Kiosco Pepito", proposedCategory: "salud", latitude: -34.6, longitude: -58.5));

            var guardado = Assert.Single(_contributions.Aportes);
            Assert.Equal(ContributionType.Correccion, guardado.Tipo);
            Assert.Equal(ContributionStatus.Pendiente, guardado.Estado);
            Assert.Equal("n123", guardado.PuntoInteresId);
            Assert.Equal("Kiosco Pepito", guardado.Nombre);
            Assert.Null(guardado.Categoria);
            Assert.Null(guardado.Ubicacion);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsLocation_SavesTheProposedPoint()
        {
            await _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Ubicacion, latitude: -34.6, longitude: -58.5));

            var location = Assert.Single(_contributions.Aportes).Ubicacion!;
            Assert.Equal(-34.6, location.Y);
            Assert.Equal(-58.5, location.X);
        }

        [Theory]
        [InlineData(CorrectionReason.Nombre)]
        [InlineData(CorrectionReason.Categoria)]
        [InlineData(CorrectionReason.Ubicacion)]
        [InlineData(CorrectionReason.Otro)]
        public async Task CreateCorrection_WhenRequiredValueIsMissing_ThrowsBadRequestException(CorrectionReason reason)
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Correction(reason)));
            Assert.Empty(_contributions.Aportes);
        }

        [Fact]
        public async Task CreateCorrection_WhenLocationIsOutsideZone_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Ubicacion, latitude: 10, longitude: -58.5)));
            Assert.Empty(_contributions.Aportes);
        }

        [Fact]
        public async Task CreateCorrection_WhenReasonIsClosed_DoesNotRequireAdditionalValues()
        {
            await _useCase.ExecuteAsync(Zone, _author, Correction(CorrectionReason.Cerro));

            Assert.Equal(CorrectionReason.Cerro, Assert.Single(_contributions.Aportes).Motivo);
        }

        [Fact]
        public async Task CreateCorrection_WhenZoneDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync("no-existe", _author, Correction(CorrectionReason.Cerro)));
            Assert.Empty(_contributions.Aportes);
        }
    }
}
