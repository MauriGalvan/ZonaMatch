using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.Resenas
{
    public class GuardarMiResenaTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _resenas = new();
        private readonly GuardarMiResena _casoDeUso;
        private readonly SesionDto _ana = new(Guid.NewGuid(), "ana@example.com");

        public GuardarMiResenaTests()
        {
            _casoDeUso = new GuardarMiResena(_resenas, new FakeZonaRepository(), new FixedTimeProvider(Ahora));
        }

        private static GuardarResenaDto Resena(int puntaje = 4, string[]? temas = null) =>
            new(puntaje, new PuntajesAspectosDto(3, 5, 4, 4, 2), "  Barrio tranquilo y bien conectado con el centro.  ", 9, temas);

        [Fact]
        public async Task Guardar_DosVeces_ReemplazaLaResenaDelUsuario()
        {
            await _casoDeUso.EjecutarAsync(Zona, _ana, Resena(puntaje: 4));
            var segunda = await _casoDeUso.EjecutarAsync(Zona, _ana, Resena(puntaje: 2, temas: ["Transporte", "seguridad"]));

            var guardada = Assert.Single(_resenas.Resenas);
            Assert.Equal(2, guardada.Puntaje);
            Assert.Equal(5, guardada.PuntajeTransporte);
            Assert.Equal("Barrio tranquilo y bien conectado con el centro.", guardada.Texto);
            Assert.Equal([AspectoZona.Seguridad, AspectoZona.Transporte], guardada.Temas);
            Assert.Equal(Ahora, guardada.FechaCreacion);
            Assert.True(segunda.EsMia);
            Assert.Equal("AN", segunda.Iniciales);
        }

        [Fact]
        public async Task Guardar_TemaDesconocido_EsInvalido()
        {
            await Assert.ThrowsAsync<DatosInvalidosException>(() => _casoDeUso.EjecutarAsync(Zona, _ana, Resena(temas: ["ruido"])));
            Assert.Empty(_resenas.Resenas);
        }

        [Fact]
        public async Task Guardar_ZonaInexistente_NoEncontrada()
        {
            await Assert.ThrowsAsync<NoEncontradoException>(() => _casoDeUso.EjecutarAsync("no-existe", _ana, Resena()));
            Assert.Empty(_resenas.Resenas);
        }
    }
}
