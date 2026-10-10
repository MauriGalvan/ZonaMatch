using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Resenas
{
    public class ListarResenasTests
    {
        private const string Zona = FakeZonaRepository.Zona;
        private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeResenaRepository _resenas = new();
        private readonly ListarResenas _casoDeUso;
        private readonly Usuario _ana = new() { Id = Guid.NewGuid(), Email = "ana@example.com" };
        private readonly Guid _luis = Guid.NewGuid();

        public ListarResenasTests()
        {
            _casoDeUso = new ListarResenas(_resenas);
        }

        // Con seguridad 3 el puntaje general es (3 + 5 + 4 + 4 + 2) / 5 = 3,6
        private Resena Agregar(Usuario autor, int seguridad = 3, string zona = Zona)
        {
            var resena = new Resena
            {
                ZonaSlug = zona,
                UsuarioId = autor.Id,
                Usuario = autor,
                PuntajeSeguridad = seguridad,
                PuntajeTransporte = 5,
                PuntajeConectividad = 4,
                PuntajeComercios = 4,
                PuntajeEspaciosVerdes = 2,
                Texto = "Barrio tranquilo y bien conectado con el centro.",
                FechaCreacion = Ahora
            };
            _resenas.Agregar(resena);
            return resena;
        }

        [Fact]
        public async Task Listar_ResumePromediosPorAspecto()
        {
            Agregar(_ana, seguridad: 3);
            Agregar(new Usuario { Id = _luis, Email = "luis@example.com" }, seguridad: 4);
            Agregar(new Usuario { Email = "mara@example.com" }, seguridad: 4);
            Agregar(_ana, seguridad: 1, zona: "otra-zona");

            var resultado = await _casoDeUso.EjecutarAsync(Zona, usuarioActual: null);

            // Puntajes generales 3,6 + 3,8 + 3,8 = 11,2 / 3 = 3,73...
            Assert.Equal(3.7, resultado.Resumen.Promedio);
            Assert.Equal(3, resultado.Resumen.Total);
            Assert.Equal(new PromedioAspectoDto(AspectoZona.Seguridad, 3.7), resultado.Resumen.Aspectos[0]);
            Assert.Equal([3.6, 3.8, 3.8], resultado.Resenas.Select(r => r.Puntaje).Order());
            Assert.All(resultado.Resenas, r => Assert.False(r.EsMia));
        }

        [Fact]
        public async Task Listar_SinResenas_PromedioCero()
        {
            var resultado = await _casoDeUso.EjecutarAsync(Zona, usuarioActual: null);

            Assert.Equal(0, resultado.Resumen.Promedio);
            Assert.Equal(AspectoZona.Todos.Count, resultado.Resumen.Aspectos.Count);
            Assert.Empty(resultado.Resenas);
        }

        [Fact]
        public async Task Listar_ConUsuario_IndicaSiEsSuyaYSiLaMarcoUtil()
        {
            var resena = Agregar(_ana);
            resena.VotosUtil.Add(new ResenaVotoUtil { ResenaId = resena.Id, UsuarioId = _luis, Fecha = Ahora });

            var paraLuis = Assert.Single((await _casoDeUso.EjecutarAsync(Zona, _luis)).Resenas);
            var paraAna = Assert.Single((await _casoDeUso.EjecutarAsync(Zona, _ana.Id)).Resenas);

            Assert.Equal(1, paraLuis.Utiles);
            Assert.True(paraLuis.MarcadaUtilPorMi);
            Assert.False(paraLuis.EsMia);
            Assert.False(paraAna.MarcadaUtilPorMi);
            Assert.True(paraAna.EsMia);
            Assert.Equal("AN", paraAna.Iniciales);
        }
    }
}
