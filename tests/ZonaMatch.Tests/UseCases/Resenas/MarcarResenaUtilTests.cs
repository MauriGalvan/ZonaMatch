using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Resenas
{
    public class MarcarResenaUtilTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _resenas = new();
        private readonly MarcarResenaUtil _casoDeUso;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();
        private readonly Resena _resenaDeAna;

        public MarcarResenaUtilTests()
        {
            _casoDeUso = new MarcarResenaUtil(_resenas, new FixedTimeProvider(Ahora));
            _resenaDeAna = new Resena { ZonaSlug = Zona, UsuarioId = _ana, FechaCreacion = Ahora };
            _resenas.Agregar(_resenaDeAna);
        }

        [Fact]
        public async Task MarcarUtil_DosVeces_GuardaUnSoloVoto()
        {
            await _casoDeUso.EjecutarAsync(Zona, _resenaDeAna.Id, _luis, util: true);
            await _casoDeUso.EjecutarAsync(Zona, _resenaDeAna.Id, _luis, util: true);

            var voto = Assert.Single(_resenaDeAna.VotosUtil);
            Assert.Equal(_luis, voto.UsuarioId);
            Assert.Equal(Ahora, voto.Fecha);
            Assert.Equal(1, _resenas.Guardados);
        }

        [Fact]
        public async Task DesmarcarUtil_QuitaElVotoYEsIdempotente()
        {
            _resenaDeAna.VotosUtil.Add(new ResenaVotoUtil { ResenaId = _resenaDeAna.Id, UsuarioId = _luis, Fecha = Ahora });

            await _casoDeUso.EjecutarAsync(Zona, _resenaDeAna.Id, _luis, util: false);
            await _casoDeUso.EjecutarAsync(Zona, _resenaDeAna.Id, _luis, util: false);

            Assert.Empty(_resenaDeAna.VotosUtil);
            Assert.Equal(1, _resenas.Guardados);
        }

        [Fact]
        public async Task MarcarUtil_TuPropiaResena_NoSePermite()
        {
            await Assert.ThrowsAsync<OperacionNoPermitidaException>(
                () => _casoDeUso.EjecutarAsync(Zona, _resenaDeAna.Id, _ana, util: true));
            Assert.Empty(_resenaDeAna.VotosUtil);
        }

        [Fact]
        public async Task MarcarUtil_ResenaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync(Zona, Guid.NewGuid(), _luis, util: true));
        }
    }
}
