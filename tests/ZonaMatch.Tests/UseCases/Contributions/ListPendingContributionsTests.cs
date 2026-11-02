using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Contributions
{
    public class ListPendingContributionsTests
    {
        private const string Zone = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeAporteRepository _contributions = new();
        private readonly ListPendingContributions _useCase;
        private readonly User _author = new() { Email = "ana.perez@example.com" };
        private readonly Guid _neighborId = Guid.NewGuid();

        public ListPendingContributionsTests()
        {
            _useCase = new ListPendingContributions(_contributions);
        }

        private Contribution Add(ContributionStatus estado = ContributionStatus.Pendiente)
        {
            var aporte = new Contribution
            {
                ZonaSlug = Zone,
                UsuarioId = _author.Id,
                User = _author,
                Tipo = ContributionType.PuntoNuevo,
                Estado = estado,
                Nombre = "Farmacia Nueva",
                FechaCreacion = Now
            };
            _contributions.Add(aporte);
            return aporte;
        }

        [Fact]
        public async Task ListPending_WhenUserHasVoted_ReturnsPendingItemsWithTheirVote()
        {
            var pendiente = Add();
            pendiente.Validar(_neighborId, confirma: false, Now);
            Add(ContributionStatus.Aprobado);

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _neighborId));

            Assert.Equal(pendiente.Id, item.Id);
            Assert.False(item.MyVote);
            Assert.False(item.IsMine);
            Assert.Equal("AP", item.AuthorInitials);
        }

        [Fact]
        public async Task ListPending_WhenUserIsTheAuthor_IndicatesOwnershipAndNoVote()
        {
            Add();

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _author.Id));

            Assert.True(item.IsMine);
            Assert.Null(item.MyVote);
        }
    }
}
