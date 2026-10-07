using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Tests.Services
{
    public class PreguntaServiceTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _preguntas = new();
        private readonly PreguntaService _sut;
        private readonly SesionDto _ana = new(Guid.NewGuid(), "ana@example.com");
        private readonly SesionDto _luis = new(Guid.NewGuid(), "luis.gomez@example.com");

        public PreguntaServiceTests()
        {
            _sut = new PreguntaService(_preguntas, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        [Fact]
        public async Task Preguntar_YResponder_ApareceEnElListado()
        {
            var pregunta = await _sut.CrearAsync(Zona, _ana, new CrearPreguntaDto("  ¿Hay verdulerias cerca de la estacion?  "));
            var creada = await _sut.ResponderAsync(Zona, pregunta.Id, _luis, new CrearRespuestaDto("Si, sobre Rivadavia."));

            var listado = await _sut.ListarAsync(Zona, _ana.Id);

            var item = Assert.Single(listado);
            Assert.Equal("¿Hay verdulerias cerca de la estacion?", item.Texto);
            Assert.True(item.EsMia);
            var respuesta = Assert.Single(item.Respuestas);
            Assert.Equal(creada.Id, respuesta.Id);
            Assert.Equal("LG", creada.Iniciales);
            Assert.False(respuesta.EsMia);
        }

        [Fact]
        public async Task Preguntar_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _sut.CrearAsync("no-existe", _ana, new CrearPreguntaDto("¿Como es el barrio de noche?")));
        }

        [Fact]
        public async Task Responder_PreguntaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _sut.ResponderAsync(Zona, Guid.NewGuid(), _luis, new CrearRespuestaDto("Hola")));
        }
    }
}
