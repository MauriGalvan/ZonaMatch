using ZonaMatch.Application.DTOs.Contributions.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class CreatePointContributionTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _contributions = new();
        private readonly CreatePointContribution _useCase;
        private readonly AuthenticatedUserContext _author = new(Guid.NewGuid(), "ana.perez@example.com");

        public CreatePointContributionTests()
        {
            _useCase = new CreatePointContribution(_contributions, new FakeZonaRepository(), new FixedTimeProvider(Now));
        }

        private static CreatePointContributionRequest Point(string category = "salud", double latitude = -34.63) =>
            new(category, " Farmacia Nueva ", "Lun a vie 9 a 20", "  ", latitude, -58.50, LivesOrWorksInZone: true);

        [Fact]
        public async Task CreatePoint_WhenValid_SavesPendingContributionWithNormalizedValues()
        {
            var response = await _useCase.ExecuteAsync(Zone, _author, Point(category: " Salud "));

            var guardado = Assert.Single(_contributions.Aportes);
            Assert.Equal(ContributionType.PuntoNuevo, guardado.Tipo);
            Assert.Equal(ContributionStatus.Pendiente, guardado.Estado);
            Assert.Equal("salud", guardado.Categoria);
            Assert.Equal("Farmacia Nueva", guardado.Nombre);
            Assert.Null(guardado.Direccion);
            Assert.Equal(-34.63, guardado.Ubicacion!.Y);
            Assert.Equal(4326, guardado.Ubicacion.SRID);
            Assert.Equal(Now, guardado.FechaCreacion);
            Assert.Equal("AP", response.AuthorInitials);
            Assert.True(response.IsMine);
        }

        [Fact]
        public async Task CreatePoint_WhenOutsideZone_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Point(latitude: 10)));
            Assert.Empty(_contributions.Aportes);
        }

        [Fact]
        public async Task CreatePoint_WhenZoneDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe", _author, Point()));
            Assert.Empty(_contributions.Aportes);
        }

        [Fact]
        public async Task CreatePoint_WhenCategoryIsUnknown_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _author, Point(category: "boliches")));
            Assert.Empty(_contributions.Aportes);
        }
    }
}
