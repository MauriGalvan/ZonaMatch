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
        private readonly Usuario _author = new() { Email = "ana.perez@example.com" };
        private readonly Guid _neighborId = Guid.NewGuid();

        public ListPendingContributionsTests()
        {
            _useCase = new ListPendingContributions(_contributions);
        }

        private Aporte Agregar(EstadoAporte estado = EstadoAporte.Pendiente)
        {
            var aporte = new Aporte
            {
                ZonaSlug = Zone,
                UsuarioId = _author.Id,
                Usuario = _author,
                Tipo = TipoAporte.PuntoNuevo,
                Estado = estado,
                Nombre = "Farmacia Nueva",
                FechaCreacion = Now
            };
            _contributions.Agregar(aporte);
            return aporte;
        }

        [Fact]
        public async Task ListPending_WhenUserHasVoted_ReturnsPendingItemsWithTheirVote()
        {
            var pendiente = Agregar();
            pendiente.Validar(_neighborId, confirma: false, Now);
            Agregar(EstadoAporte.Aprobado);

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _neighborId));

            Assert.Equal(pendiente.Id, item.Id);
            Assert.False(item.MyVote);
            Assert.False(item.IsMine);
            Assert.Equal("AP", item.AuthorInitials);
        }

        [Fact]
        public async Task ListPending_WhenUserIsTheAuthor_IndicatesOwnershipAndNoVote()
        {
            Agregar();

            var item = Assert.Single(await _useCase.ExecuteAsync(Zone, _author.Id));

            Assert.True(item.IsMine);
            Assert.Null(item.MyVote);
        }
    }
}
