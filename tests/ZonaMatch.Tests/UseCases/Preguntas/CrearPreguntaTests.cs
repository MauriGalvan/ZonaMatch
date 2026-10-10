using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Preguntas;

namespace ZonaMatch.Tests.UseCases.Preguntas
{
    public class CrearPreguntaTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _preguntas = new();
        private readonly CrearPregunta _casoDeUso;
        private readonly SesionDto _ana = new(Guid.NewGuid(), "ana@example.com");

        public CrearPreguntaTests()
        {
            _casoDeUso = new CrearPregunta(_preguntas, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        [Fact]
        public async Task Preguntar_GuardaElTextoSinEspacios()
        {
            var creada = await _casoDeUso.EjecutarAsync(Zona, _ana, new CrearPreguntaDto("  ¿Hay verdulerias cerca de la estacion?  "));

            var guardada = Assert.Single(_preguntas.Preguntas);
            Assert.Equal("¿Hay verdulerias cerca de la estacion?", guardada.Texto);
            Assert.Equal(_ana.Id, guardada.UsuarioId);
            Assert.Equal(Ahora, guardada.FechaCreacion);
            Assert.Equal(guardada.Id, creada.Id);
            Assert.Equal("AN", creada.Iniciales);
            Assert.True(creada.EsMia);
            Assert.Empty(creada.Respuestas);
        }

        [Fact]
        public async Task Preguntar_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(
                () => _casoDeUso.EjecutarAsync("no-existe", _ana, new CrearPreguntaDto("¿Como es el barrio de noche?")));
            Assert.Empty(_preguntas.Preguntas);
        }
    }
}
