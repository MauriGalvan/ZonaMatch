using ZonaMatch.Application.UseCases.Preguntas;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Preguntas
{
    public class ListarPreguntasTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakePreguntaRepository _preguntas = new();
        private readonly ListarPreguntas _casoDeUso;
        private readonly Usuario _ana = new() { Email = "ana@example.com" };
        private readonly Usuario _luis = new() { Email = "luis.gomez@example.com" };

        public ListarPreguntasTests()
        {
            _casoDeUso = new ListarPreguntas(_preguntas);
        }

        private Respuesta Respuesta(Pregunta pregunta, string texto, DateTimeOffset fecha) =>
            new() { PreguntaId = pregunta.Id, UsuarioId = _luis.Id, Usuario = _luis, Texto = texto, FechaCreacion = fecha };

        [Fact]
        public async Task Listar_IndicaLasPropiasYOrdenaLasRespuestasDeLaMasVieja()
        {
            var pregunta = new Pregunta { ZonaSlug = Zona, UsuarioId = _ana.Id, Usuario = _ana, Texto = "¿Hay verdulerias?", FechaCreacion = Ahora };
            _preguntas.Agregar(pregunta);
            _preguntas.Agregar(Respuesta(pregunta, "Segunda", Ahora.AddHours(2)));
            _preguntas.Agregar(Respuesta(pregunta, "Primera", Ahora.AddHours(1)));
            _preguntas.Agregar(new Pregunta { ZonaSlug = "otra-zona", UsuarioId = _ana.Id, Texto = "¿Y aca?" });

            var listado = await _casoDeUso.EjecutarAsync(Zona, _ana.Id);

            var item = Assert.Single(listado);
            Assert.True(item.EsMia);
            Assert.Equal("AN", item.Iniciales);
            Assert.Equal(["Primera", "Segunda"], item.Respuestas.Select(r => r.Texto));
            Assert.All(item.Respuestas, r =>
            {
                Assert.False(r.EsMia);
                Assert.Equal("LG", r.Iniciales);
            });
        }

        [Fact]
        public async Task Listar_SinSesion_NadaEsPropio()
        {
            _preguntas.Agregar(new Pregunta { ZonaSlug = Zona, UsuarioId = _ana.Id, Usuario = _ana, Texto = "¿Hay verdulerias?" });

            var item = Assert.Single(await _casoDeUso.EjecutarAsync(Zona, usuarioActual: null));

            Assert.False(item.EsMia);
        }
    }
}
