using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Preguntas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Preguntas
{
    public class ResponderPreguntaTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _preguntas = new();
        private readonly ResponderPregunta _casoDeUso;
        private readonly SesionDto _luis = new(Guid.NewGuid(), "luis.gomez@example.com");
        private readonly Pregunta _pregunta = new() { ZonaSlug = Zona, UsuarioId = Guid.NewGuid(), Texto = "¿Hay verdulerias?" };

        public ResponderPreguntaTests()
        {
            _casoDeUso = new ResponderPregunta(_preguntas, new FixedTimeProvider(Ahora));
            _preguntas.Agregar(_pregunta);
        }

        [Fact]
        public async Task Responder_AgregaLaRespuestaALaPregunta()
        {
            var creada = await _casoDeUso.EjecutarAsync(Zona, _pregunta.Id, _luis, new CrearRespuestaDto("  Si, sobre Rivadavia. "));

            var guardada = Assert.Single(_pregunta.Respuestas);
            Assert.Equal("Si, sobre Rivadavia.", guardada.Texto);
            Assert.Equal(_luis.Id, guardada.UsuarioId);
            Assert.Equal(Ahora, guardada.FechaCreacion);
            Assert.Equal(guardada.Id, creada.Id);
            Assert.Equal("LG", creada.Iniciales);
            Assert.True(creada.EsMia);
        }

        [Fact]
        public async Task Responder_PreguntaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _casoDeUso.EjecutarAsync(Zona, Guid.NewGuid(), _luis, new CrearRespuestaDto("Hola")));
        }

        [Fact]
        public async Task Responder_PreguntaDeOtraZona_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _casoDeUso.EjecutarAsync("otra-zona", _pregunta.Id, _luis, new CrearRespuestaDto("Hola")));
            Assert.Empty(_pregunta.Respuestas);
        }
    }
}
