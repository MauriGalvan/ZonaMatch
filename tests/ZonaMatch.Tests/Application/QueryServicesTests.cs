using NSubstitute;
using ZonaMatch.Application.Common;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.PointsOfInterest;
using ZonaMatch.Application.Territory;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Application
{
    public class IndicatorServiceTests
    {
        private readonly IIndicatorRepository _repository = Substitute.For<IIndicatorRepository>();

        [Fact]
        public async Task Sin_codigos_o_sin_zonas_no_consulta()
        {
            var service = new IndicatorService(_repository);

            var noCodes = await service.ResolveAsync([TestData.Zone(1, "a", -58.5, 10)], [], CancellationToken.None);
            var noZones = await service.ResolveAsync([], ["population"], CancellationToken.None);

            Assert.Empty(noCodes[1]);
            Assert.Empty(noZones);
            await _repository.DidNotReceiveWithAnyArgs().GetAsync(default!, default!, default);
        }

        [Fact]
        public async Task Usa_el_dato_propio_mas_reciente_o_hereda_de_la_unidad_superior()
        {
            var own = TestData.Zone(1, "ramos", -58.56, unitId: 10, parentUnitId: 100);
            var inheritsFromNamedParent = TestData.Zone(2, "haedo", -58.6, unitId: 20, parentUnitId: 100);
            var inheritsWithoutParentName = TestData.Zone(3, "luro", -58.5, unitId: 30, parentUnitId: 300, parentName: "CABA", parentUnitName: null);
            var orphan = TestData.Zone(4, "sin-padre", -58.4, unitId: 40);

            _repository.GetAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(
                [
                    new IndicatorData(10, "crime", 30, "c/1000", "SNIC", new DateOnly(2024, 1, 1)),
                    new IndicatorData(10, "crime", 27, "c/1000", "SNIC", new DateOnly(2025, 1, 1)),
                    new IndicatorData(100, "crime", 45, "c/1000", "SNIC", new DateOnly(2025, 1, 1)),
                    new IndicatorData(300, "crime", 36, "c/1000", "GCBA", new DateOnly(2025, 1, 1)),
                ]);

            var result = await new IndicatorService(_repository).ResolveAsync(
                [own, inheritsFromNamedParent, inheritsWithoutParentName, orphan], ["crime"], CancellationToken.None);

            Assert.Equal(new IndicatorValueDto("crime", 27, "c/1000", "SNIC", new DateOnly(2025, 1, 1), false, "Zona"), result[1]["crime"]);
            Assert.Equal(new IndicatorValueDto("crime", 45, "c/1000", "SNIC", new DateOnly(2025, 1, 1), true, "La Matanza"), result[2]["crime"]);
            Assert.Equal("CABA", result[3]["crime"].Level);
            Assert.True(result[3]["crime"].Inherited);
            Assert.Empty(result[4]);
        }
    }

    public class ZoneQueryServiceTests
    {
        private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();
        private readonly ITerritorialUnitRepository _units = Substitute.For<ITerritorialUnitRepository>();
        private readonly IIndicatorRepository _indicators = Substitute.For<IIndicatorRepository>();
        private readonly ZoneQueryService _service;

        public ZoneQueryServiceTests()
        {
            _service = new ZoneQueryService(_zones, _units, new IndicatorService(_indicators));
            _indicators.GetAsync(default!, default!, default).ReturnsForAnyArgs([]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(51)]
        public async Task SearchAsync_valida_el_limite(int limit)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.SearchAsync("x", limit, CancellationToken.None));
        }

        [Fact]
        public async Task SearchAsync_normaliza_la_busqueda()
        {
            _zones.SearchAsync("RAMOS MEJIA", 5, Arg.Any<CancellationToken>())
                .Returns([new ZoneSummary(1, "ramos-mejia-la-matanza", "Ramos Mejía", "La Matanza", TerritorialUnitType.Localidad)]);

            var result = await _service.SearchAsync(" ramos  mejía", 5, CancellationToken.None);

            Assert.Equal(new ZoneSummaryDto("ramos-mejia-la-matanza", "Ramos Mejía", "La Matanza", "Ramos Mejía, La Matanza", "localidad"), Assert.Single(result));
        }

        [Fact]
        public async Task GetDetailAsync_devuelve_la_ficha_o_404()
        {
            var zone = TestData.Zone(1, "villa-luro-caba", -58.5, 10, 100, "Villa Luro", "CABA", "Comuna 10", TerritorialUnitType.Barrio);
            _zones.GetBySlugAsync("villa-luro-caba", Arg.Any<CancellationToken>()).Returns(zone);
            _indicators.GetAsync(default!, default!, default).ReturnsForAnyArgs(
                [new IndicatorData(10, IndicatorCodes.Population, 34000, "hab", "INDEC", new DateOnly(2022, 5, 18))]);

            var detail = await _service.GetDetailAsync("villa-luro-caba", CancellationToken.None);

            Assert.Equal("Villa Luro, CABA", detail.DisplayName);
            Assert.Equal("barrio", detail.Type);
            Assert.Equal("Comuna 10", detail.ContainerName);
            Assert.Equal(1.23d, detail.AreaKm2);
            Assert.Equal(zone.Center.Y, detail.Center.Latitude);
            Assert.Same(zone.Geometry, detail.Geometry);
            Assert.Equal(34000m, Assert.Single(detail.Indicators).Value);
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetDetailAsync("no-existe", CancellationToken.None));
        }

        [Fact]
        public async Task GetNeighborsAsync_lista_las_aledanas()
        {
            _zones.GetBySlugAsync("ramos", Arg.Any<CancellationToken>()).Returns(TestData.Zone(1, "ramos", -58.56, 10));
            _zones.GetNeighborsAsync(1, Arg.Any<CancellationToken>())
                .Returns([new ZoneSummary(2, "haedo-moron", "Haedo", "Morón", TerritorialUnitType.Localidad)]);

            var result = await _service.GetNeighborsAsync("ramos", CancellationToken.None);

            Assert.Equal("haedo-moron", Assert.Single(result).Slug);
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetNeighborsAsync("no-existe", CancellationToken.None));
        }

        [Theory]
        [InlineData(-91, 0)]
        [InlineData(91, 0)]
        [InlineData(0, -181)]
        [InlineData(0, 181)]
        public async Task GetContextAsync_valida_coordenadas(double latitude, double longitude)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetContextAsync(latitude, longitude, CancellationToken.None));
        }

        [Fact]
        public async Task GetContextAsync_devuelve_zona_departamento_y_radio()
        {
            _units.FindContainingAsync(default!, default!, default).ReturnsForAnyArgs(
                    new TerritorialUnitSummary(11, TerritorialUnitType.Localidad, null, "Ramos Mejía", 1),
                    new TerritorialUnitSummary(1, TerritorialUnitType.Partido, "427", "La Matanza", null),
                    new TerritorialUnitSummary(500, TerritorialUnitType.CensusTract, "064270206", "064270206", 1));
            _zones.GetByTerritorialUnitIdAsync(11, Arg.Any<CancellationToken>())
                .Returns(new ZoneSummary(3, "ramos-mejia-la-matanza", "Ramos Mejía", "La Matanza", TerritorialUnitType.Localidad));

            var context = await _service.GetContextAsync(-34.6455, -58.564, CancellationToken.None);

            Assert.Equal(new CoordinatesDto(-34.6455, -58.564), context.Point);
            Assert.Equal("ramos-mejia-la-matanza", context.Zone!.Slug);
            Assert.Equal(new TerritorialUnitDto("Ramos Mejía", "localidad", null), context.ZoneUnit);
            Assert.Equal(new TerritorialUnitDto("La Matanza", "partido", "427"), context.Department);
            Assert.Equal(new TerritorialUnitDto("064270206", "radio_censal", "064270206"), context.CensusTract);
        }

        [Fact]
        public async Task GetContextAsync_tolera_puntos_fuera_de_las_capas()
        {
            _units.FindContainingAsync(default!, default!, default).ReturnsForAnyArgs(
                new TerritorialUnitSummary(11, TerritorialUnitType.Barrio, null, "Barrio sin zona", 1),
                (TerritorialUnitSummary?)null,
                (TerritorialUnitSummary?)null);

            var withUnitButNoZone = await _service.GetContextAsync(-34.6, -58.4, CancellationToken.None);

            Assert.Null(withUnitButNoZone.Zone);
            Assert.NotNull(withUnitButNoZone.ZoneUnit);
            Assert.Null(withUnitButNoZone.Department);

            _units.FindContainingAsync(default!, default!, default).ReturnsForAnyArgs((TerritorialUnitSummary?)null);
            var outside = await _service.GetContextAsync(10, 10, CancellationToken.None);

            Assert.Null(outside.Zone);
            Assert.Null(outside.ZoneUnit);
            await _zones.ReceivedWithAnyArgs(1).GetByTerritorialUnitIdAsync(default, default);
        }

        [Theory]
        [InlineData(TerritorialUnitType.Barrio, "barrio")]
        [InlineData(TerritorialUnitType.Localidad, "localidad")]
        [InlineData(TerritorialUnitType.Comuna, "comuna")]
        [InlineData(TerritorialUnitType.Partido, "partido")]
        [InlineData(TerritorialUnitType.CensusTract, "radio_censal")]
        public void TypeName_por_tipo(TerritorialUnitType type, string expected)
        {
            Assert.Equal(expected, ZoneSummaryDto.TypeName(type));
        }

        [Fact]
        public void TypeName_rechaza_tipos_desconocidos()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ZoneSummaryDto.TypeName((TerritorialUnitType)42));
        }
    }

    public class PoiQueryServiceTests
    {
        private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();
        private readonly IPoiRepository _pois = Substitute.For<IPoiRepository>();
        private readonly IPoiCatalogRepository _catalog = Substitute.For<IPoiCatalogRepository>();
        private readonly PoiQueryService _service;

        public PoiQueryServiceTests()
        {
            _service = new PoiQueryService(_zones, _pois, _catalog);
            _zones.GetBySlugAsync("luro", Arg.Any<CancellationToken>()).Returns(TestData.Zone(1, "luro", -58.5, 10));
            _catalog.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(
            [
                TestData.WithId(new PoiCategory("salud", "Salud", null, 3), (short)3),
                TestData.WithId(new PoiCategory("educacion", "Educación", null, 2), (short)2),
                TestData.WithId(new PoiCategory("educacion.secundaria", "Secundaria", 2, 3), (short)203),
                TestData.WithId(new PoiCategory("educacion.jardin", "Jardín", 2, 1), (short)201),
            ]);
        }

        [Theory]
        [InlineData(99)]
        [InlineData(5001)]
        public void ValidateRadius_rechaza_radios_fuera_de_rango(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PoiQueryService.ValidateRadius(radius));
        }

        [Fact]
        public async Task GetCategoriesAsync_arma_el_arbol_ordenado()
        {
            var result = await _service.GetCategoriesAsync(CancellationToken.None);

            Assert.Equal(["educacion", "salud"], result.Select(category => category.Code));
            Assert.Equal(["educacion.jardin", "educacion.secundaria"], result[0].Subcategories.Select(category => category.Code));
            Assert.Empty(result[1].Subcategories);
        }

        [Fact]
        public async Task GetNearbyAsync_devuelve_pois_con_trazabilidad()
        {
            _pois.FindWithinAsync(Arg.Any<NetTopologySuite.Geometries.Point>(), 1000, Arg.Any<IReadOnlyCollection<string>>(), PoiQueryService.MaxResults, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new PoiData(1, "EP 39", "Av. de Mayo 700", "educacion.primaria", "Primaria", "educacion", Ownership.Public, TestGeometry.Point(-58.5, -34.6), "Padrón", new DateOnly(2026, 3, 1), 120.4),
                    new PoiData(2, "Colegio", null, "educacion.secundaria", "Secundaria", "educacion", Ownership.Private, TestGeometry.Point(-58.5, -34.6), "OSM", null, 300.6),
                    new PoiData(3, "Plaza", null, "espacios_verdes.plaza_parque", "Plaza", "espacios_verdes", Ownership.Unknown, TestGeometry.Point(-58.5, -34.6), "OSM", null, 10),
                ]);

            var result = await _service.GetNearbyAsync("luro", 1000, ["educacion"], CancellationToken.None);

            Assert.Equal(["publica", "privada", "desconocida"], result.Select(poi => poi.Ownership));
            Assert.Equal(120d, result[0].DistanceMeters);
            Assert.Equal("educacion", result[0].Layer);
            Assert.Equal(-34.6, result[0].Location.Latitude);
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetNearbyAsync("no-existe", 1000, [], CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetNearbyAsync("luro", 10, [], CancellationToken.None));
        }

        [Fact]
        public async Task GetSummaryAsync_cuenta_por_capa_y_subcategoria()
        {
            _pois.CountWithinAsync(Arg.Any<NetTopologySuite.Geometries.Point>(), 500, Arg.Any<CancellationToken>())
                .Returns([new PoiCategoryCount("educacion", "educacion.jardin", 6), new PoiCategoryCount("educacion", "educacion.secundaria", 5)]);

            var summary = await _service.GetSummaryAsync("luro", 500, CancellationToken.None);

            Assert.Equal("luro", summary.Slug);
            Assert.Equal(500, summary.RadiusMeters);
            Assert.Equal(11, summary.Layers[0].Count);
            Assert.Equal(0, summary.Layers[1].Count);
            Assert.Equal(new SubcategoryCountDto("educacion.jardin", "Jardín", 6), summary.Layers[0].Subcategories[0]);
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetSummaryAsync("no-existe", 500, CancellationToken.None));
        }
    }
}
