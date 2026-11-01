using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Perfil;

namespace ZonaMatch.Tests.UseCases.SavedAnalyses
{
    public class SavedAnalysesTests
    {
        private static readonly DateTimeOffset Ahora = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid Usuario = Guid.NewGuid();

        private readonly FakePerfilRepository _perfiles = new();
        private readonly FakeAnalisisRepository _analisis = new();
        private readonly FakeCriterioRepository _criterios = new();
        private readonly FakeZonaRepository _zonas = new();
        private readonly FixedTimeProvider _reloj = new(Ahora);
        private readonly FakeResultCalculator _resultados = new();

        private CreateAnalysis Crear => new(_analisis, _perfiles, _zonas, _reloj, _resultados);
        private CreateAnalysisVersion Versionar => new(_analisis, _criterios, _zonas, _reloj, _resultados);
        private RecalculateAnalysis Recalcular => new(_analisis, _resultados, _reloj);

        private async Task ConPerfil(string criterio = "seguridad", string prioridad = "alta") =>
            await new GuardarMiPerfil(_perfiles, _criterios, _reloj)
                .EjecutarAsync(Usuario, Pedidos.Criterio(new() { [criterio] = prioridad }));

        private static CreateAnalysisRequest Pedido(string name = "Cerca del trabajo", params AnalysisZoneRequest[] zones) =>
            new() { Name = name, Zones = zones.Length > 0 ? [.. zones] : [Pedidos.Zona(inicial: true)] };

        [Fact]
        public async Task Create_WhenSaved_KeepsTheResultAndWhenItWasCalculated()
        {
            await ConPerfil();
            _resultados.Puntaje = 81;

            var dto = await Crear.ExecuteAsync(Usuario, Pedido());

            Assert.Equal(81, Assert.Single(dto.Result!.Zonas).Puntaje);
            Assert.Equal(Ahora, dto.CalculatedAt);
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisIsSaved_CreatesAVersionWithTodaysResultAndKeepsTheOriginal()
        {
            await ConPerfil();
            _resultados.Puntaje = 81;
            var original = await Crear.ExecuteAsync(Usuario, Pedido());
            _resultados.Puntaje = 74;

            var recalculated = await Recalcular.ExecuteAsync(original.Id, Usuario);

            Assert.Equal(original.Id, recalculated.OriginAnalysisId);
            Assert.Equal("Cerca del trabajo · datos al 14/10", recalculated.Name);
            Assert.Equal(74, Assert.Single(recalculated.Result!.Zonas).Puntaje);
            Assert.Equal(original.Criteria, recalculated.Criteria);
            var saved = _analisis.Analisis.Single(a => a.Id == original.Id);
            Assert.Equal(81, AnalysisResultCalculator.Deserialize(saved.ResultadoJson)!.Zonas.Single().Puntaje);
        }

        [Fact]
        public async Task Recalculate_WhenAnalysisDoesNotExist_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Recalcular.ExecuteAsync(Guid.NewGuid(), Usuario));
        }

        private static AnalysisZoneRequest ZonaConOrigen(double latitude) =>
            new()
            {
                Slug = FakeZonaRepository.Zona, Name = "Villa Luro, CABA", IsInitial = true,
                Origin = new AnalysisZoneOriginRequest { Latitude = latitude, Longitude = -58.5 }
            };

        [Fact]
        public async Task Create_WithAStartingPoint_SavesItAndCalculatesFromIt()
        {
            await ConPerfil();

            var dto = await Crear.ExecuteAsync(Usuario, Pedido("Con punto", ZonaConOrigen(-34.63)));

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(dto.Zones).Origin);
            var calculated = Assert.Single(Assert.Single(_resultados.Calculados).Zonas);
            Assert.Equal((-34.63, -58.5), (calculated.OrigenLatitud, calculated.OrigenLongitud));
        }

        [Fact]
        public async Task Create_WithAStartingPointOutsideTheZone_ThrowsBadRequestException()
        {
            await ConPerfil();

            await Assert.ThrowsAsync<BadRequestException>(() => Crear.ExecuteAsync(Usuario, Pedido("Afuera", ZonaConOrigen(10))));
            Assert.Empty(_analisis.Analisis);
        }

        [Fact]
        public async Task Recalculate_KeepsTheStartingPointsOfTheZones()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido("Con punto", ZonaConOrigen(-34.63)));

            var recalculated = await Recalcular.ExecuteAsync(original.Id, Usuario);

            Assert.Equal(new AnalysisZoneOriginResponse(-34.63, -58.5), Assert.Single(recalculated.Zones).Origin);
        }

        private sealed class FakeResultCalculator : IAnalysisResultCalculator
        {
            public int? Puntaje { get; set; } = 50;

            // Analyses received, to check what the calculation used
            public List<Analisis> Calculados { get; } = [];

            public Task<PuntajesDto?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default)
            {
                Calculados.Add(analysis);
                return Task.FromResult<PuntajesDto?>(new PuntajesDto(
                    "transporte-publico",
                    analysis.Zonas.Select(zone => new PuntajeZonaDto(zone.ZonaSlug, zone.Nombre, Puntaje, [], [], [], null)).ToList(),
                    []));
            }
        }

        [Fact]
        public async Task Crear_SinPerfil_LanzaPerfilNoEncontrado()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Crear.ExecuteAsync(Usuario, Pedido()));
            Assert.Empty(_analisis.Analisis);
        }

        [Fact]
        public async Task Crear_GuardaUnaCopiaDelPerfilQueNoCambiaSiDespuesSeEditaElPerfil()
        {
            await ConPerfil("seguridad", "alta");

            var dto = await Crear.ExecuteAsync(Usuario, Pedido());

            var analisis = Assert.Single(_analisis.Analisis);
            var perfil = Assert.Single(_perfiles.Perfiles);
            Assert.NotSame(perfil, analisis.Contexto);
            Assert.Equal(TipoContexto.Analisis, analisis.Contexto.Tipo);
            Assert.Null(analisis.AnalisisOrigenId);
            Assert.Equal("alta", dto.Criteria["seguridad"]);
            Assert.True(Assert.Single(dto.Zones).IsInitial);

            await ConPerfil("salud", "baja");

            Assert.Equal("seguridad", Assert.Single(analisis.Contexto.Criterios).CriterioId);
        }

        [Fact]
        public async Task Crear_ZonaQueNoExiste_LanzaNoEncontrado()
        {
            await ConPerfil();

            await Assert.ThrowsAsync<NotFoundException>(
                () => Crear.ExecuteAsync(Usuario, Pedido(zones: Pedidos.Zona("no-existe"))));
            Assert.Empty(_analisis.Analisis);
        }

        [Fact]
        public async Task Crear_ZonasRepetidas_LanzaError()
        {
            await ConPerfil();

            var error = await Assert.ThrowsAsync<BadRequestException>(
                () => Crear.ExecuteAsync(Usuario, Pedido(zones: [Pedidos.Zona(), Pedidos.Zona()])));
            Assert.Contains("repetidas", error.Message);
        }

        [Fact]
        public async Task Version_CuelgaDelOriginalConElCriterioAjustadoYNombreNumerado()
        {
            await ConPerfil("seguridad", "alta");
            var original = await Crear.ExecuteAsync(Usuario, Pedido("Cerca del trabajo"));

            var version = await Versionar.ExecuteAsync(original.Id, Usuario, new CreateAnalysisVersionRequest
            {
                Criterion = Pedidos.Criterio(new() { ["salud"] = "alta" }),
            });

            Assert.Equal(original.Id, version.OriginAnalysisId);
            Assert.Equal("Cerca del trabajo (2)", version.Name);
            Assert.Equal("alta", version.Criteria["salud"]);
            // When the request has no zones, compare the same zones as the original.
            Assert.Equal(FakeZonaRepository.Zona, Assert.Single(version.Zones).Slug);
            // The original remains unchanged.
            var guardado = _analisis.Analisis.Single(a => a.Id == original.Id);
            Assert.Equal("seguridad", Assert.Single(guardado.Contexto.Criterios).CriterioId);
        }

        [Fact]
        public async Task Version_DeUnaVersion_TambienCuelgaDelOriginal()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido("Facu"));
            var primera = await Versionar.ExecuteAsync(original.Id, Usuario, new() { Criterion = Pedidos.Criterio() });

            var segunda = await Versionar.ExecuteAsync(primera.Id, Usuario, new() { Criterion = Pedidos.Criterio() });

            Assert.Equal(original.Id, segunda.OriginAnalysisId);
            Assert.Equal("Facu (3)", segunda.Name);
        }

        [Fact]
        public async Task Version_DeOtroUsuario_LanzaNoEncontrado()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido());

            await Assert.ThrowsAsync<NotFoundException>(
                () => Versionar.ExecuteAsync(original.Id, Guid.NewGuid(), new() { Criterion = Pedidos.Criterio() }));
        }

        [Fact]
        public async Task Listar_DevuelveLosOriginalesConSusVersionesAdentro()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido("A"));
            await Crear.ExecuteAsync(Usuario, Pedido("B"));
            await Versionar.ExecuteAsync(original.Id, Usuario, new() { Criterion = Pedidos.Criterio() });

            var listado = await new ListMyAnalyses(_analisis).ExecuteAsync(Usuario);

            Assert.Equal(2, listado.Count);
            var a = listado.Single(x => x.Name == "A");
            Assert.Equal("A (2)", Assert.Single(a.Versions).Name);
            Assert.Empty(listado.Single(x => x.Name == "B").Versions);
        }

        [Fact]
        public async Task Eliminar_UnOriginal_BorraTambienSusVersiones()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido());
            await Versionar.ExecuteAsync(original.Id, Usuario, new() { Criterion = Pedidos.Criterio() });
            var otro = await Crear.ExecuteAsync(Usuario, Pedido("Otro"));

            await new DeleteAnalysis(_analisis).ExecuteAsync(original.Id, Usuario);

            Assert.Equal(otro.Id, Assert.Single(_analisis.Analisis).Id);
        }

        [Fact]
        public async Task Renombrar_SoloCambiaElNombre()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido("Viejo"));

            var dto = await new RenameAnalysis(_analisis, _reloj)
                .ExecuteAsync(original.Id, Usuario, new RenameAnalysisRequest { Name = "  Nuevo " });

            Assert.Equal("Nuevo", dto.Name);
            Assert.Equal(original.Criteria, dto.Criteria);
        }

        [Fact]
        public async Task Favorito_SeGuardaSinTocarLaFechaDeActualizacion()
        {
            await ConPerfil();
            var original = await Crear.ExecuteAsync(Usuario, Pedido("Favorito"));
            var useCase = new SetAnalysisFavorite(_analisis);

            await useCase.ExecuteAsync(original.Id, Usuario, true);
            var marcado = _analisis.Analisis.Single(a => a.Id == original.Id);

            Assert.True(marcado.EsFavorito);
            Assert.Equal(original.UpdatedAt, marcado.FechaActualizacion);
            await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(original.Id, Guid.NewGuid(), true));
        }

        [Fact]
        public void BuildVersionName_TrimsToStayWithinMaximumLength()
        {
            var nombre = CreateAnalysisVersion.BuildVersionName(new string('a', 50), 12);

            Assert.Equal(50, nombre.Length);
            Assert.EndsWith(" (12)", nombre);
        }
    }
}
