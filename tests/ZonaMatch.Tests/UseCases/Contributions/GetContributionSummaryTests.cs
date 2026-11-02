using ZonaMatch.Application.DTOs.Contributions.Responses;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class GetContributionSummaryTests
    {
        private const string Zone = FakeZonaRepository.Zona;

        private readonly FakeAporteRepository _contributions = new();
        private readonly GetContributionSummary _useCase;

        public GetContributionSummaryTests()
        {
            _useCase = new GetContributionSummary(_contributions);
        }

        private void Add(EstadoAporte status, string zone = Zone) =>
            _contributions.Agregar(new Aporte { ZonaSlug = zone, UsuarioId = Guid.NewGuid(), Estado = status });

        [Fact]
        public async Task GetSummary_CountsApprovedAndPendingButNotRejectedContributions()
        {
            Add(EstadoAporte.Aprobado);
            Add(EstadoAporte.Pendiente);
            Add(EstadoAporte.Pendiente);
            Add(EstadoAporte.Rechazado);
            Add(EstadoAporte.Aprobado, zone: "other-zone");

            Assert.Equal(new ContributionSummaryResponse(3, 1, 2), await _useCase.ExecuteAsync(Zone));
        }
    }
}
