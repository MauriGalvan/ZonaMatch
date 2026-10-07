using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Services
{
    public class ResenaServiceTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _resenas = new();
        private readonly ResenaService _sut;
        private readonly SesionDto _ana = new(Guid.NewGuid(), "ana@example.com");
        private readonly SesionDto _luis = new(Guid.NewGuid(), "luis@example.com");

        public ResenaServiceTests()
        {
            _sut = new ResenaService(_resenas, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        private static GuardarResenaDto Resena(int puntaje = 4, string[]? temas = null, int seguridad = 3) =>
            new(puntaje, new PuntajesAspectosDto(seguridad, 5, 4, 4, 2), "  Barrio tranquilo y bien conectado con el centro.  ", 9, temas);

        [Fact]
        public async Task Guardar_DosVeces_ReemplazaLaResenaDelUsuario()
        {
            await _sut.GuardarMiaAsync(Zona, _ana, Resena(puntaje: 4));
            var segunda = await _sut.GuardarMiaAsync(Zona, _ana, Resena(puntaje: 2, temas: ["Transporte", "seguridad"]));

            var guardada = Assert.Single(_resenas.Resenas);
            Assert.Equal(2, guardada.Puntaje);
            Assert.Equal("Barrio tranquilo y bien conectado con el centro.", guardada.Texto);
            Assert.Equal([AspectoZona.Seguridad, AspectoZona.Transporte], guardada.Temas);
            Assert.True(segunda.EsMia);
            Assert.Equal("AN", segunda.Iniciales);
        }

        [Fact]
        public async Task Guardar_TemaDesconocido_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _sut.GuardarMiaAsync(Zona, _ana, Resena(temas: ["ruido"])));
            Assert.Empty(_resenas.Resenas);
        }

        [Fact]
        public async Task Guardar_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _sut.GuardarMiaAsync("no-existe", _ana, Resena()));
        }

        [Fact]
        public async Task Listar_ResumePromediosPorAspecto()
        {
            await _sut.GuardarMiaAsync(Zona, _ana, Resena(puntaje: 4, seguridad: 3));
            await _sut.GuardarMiaAsync(Zona, _luis, Resena(puntaje: 5, seguridad: 4));

            var resultado = await _sut.ListarAsync(Zona, usuarioActual: null);

            Assert.Equal(4.5, resultado.Resumen.Promedio);
            Assert.Equal(2, resultado.Resumen.Total);
            Assert.Equal(new PromedioAspectoDto(AspectoZona.Seguridad, 3.5), resultado.Resumen.Aspectos[0]);
            Assert.All(resultado.Resenas, r => Assert.False(r.EsMia));
        }

        [Fact]
        public async Task Listar_SinResenas_PromedioCero()
        {
            var resultado = await _sut.ListarAsync(Zona, usuarioActual: null);

            Assert.Equal(0, resultado.Resumen.Promedio);
            Assert.Equal(AspectoZona.Todos.Count, resultado.Resumen.Aspectos.Count);
        }

        [Fact]
        public async Task MarcarUtil_EsIdempotenteYSePuedeQuitar()
        {
            var resena = await _sut.GuardarMiaAsync(Zona, _ana, Resena());

            await _sut.MarcarUtilAsync(Zona, resena.Id, _luis.Id, util: true);
            await _sut.MarcarUtilAsync(Zona, resena.Id, _luis.Id, util: true);
            var marcada = (await _sut.ListarAsync(Zona, _luis.Id)).Resenas.Single();

            await _sut.MarcarUtilAsync(Zona, resena.Id, _luis.Id, util: false);
            var desmarcada = (await _sut.ListarAsync(Zona, _luis.Id)).Resenas.Single();

            Assert.Equal(1, marcada.Utiles);
            Assert.True(marcada.MarcadaUtilPorMi);
            Assert.Equal(0, desmarcada.Utiles);
        }

        [Fact]
        public async Task MarcarUtil_TuPropiaResena_NoSePermite()
        {
            var resena = await _sut.GuardarMiaAsync(Zona, _ana, Resena());

            await Assert.ThrowsAsync<OperacionNoPermitidaException>(() => _sut.MarcarUtilAsync(Zona, resena.Id, _ana.Id, util: true));
        }

        [Fact]
        public async Task Eliminar_SinResena_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _sut.EliminarMiaAsync(Zona, _ana.Id));
        }
    }
}
