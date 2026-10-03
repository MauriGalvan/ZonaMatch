using NSubstitute;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Application
{
    public class TerritorialImportServiceTests
    {
        private readonly ITerritorialSourceReader _reader = Substitute.For<ITerritorialSourceReader>();
        private readonly ITerritorialUnitRepository _units = Substitute.For<ITerritorialUnitRepository>();
        private readonly IDataSourceRepository _dataSources = Substitute.For<IDataSourceRepository>();
        private readonly List<IReadOnlyList<TerritorialUnit>> _upserts = [];

        public TerritorialImportServiceTests()
        {
            _dataSources.GetByCodeAsync("zm-radios", Arg.Any<CancellationToken>())
                .Returns(TestData.WithId(new DataSource("zm-radios", "Radios", DataSourceKind.Official, null, null, null), (short)3));
            _units.UpsertAsync(Arg.Any<short>(), Arg.Do<IReadOnlyList<TerritorialUnit>>(_upserts.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }

        private TerritorialImportService Service(params TerritorialSourceOptions[] sources) =>
            new(_reader, _units, _dataSources, TestData.Clock(), TestData.Options(options =>
            {
                options.BatchSize = 2;
                options.TerritorialSources = [.. sources];
            }));

        private static TerritorialSourceOptions Source(TerritorialUnitType? parentType = null, string? parentCodeColumn = null) => new()
        {
            DataSourceCode = "zm-radios",
            Type = TerritorialUnitType.CensusTract,
            ParentType = parentType,
            ParentCodeColumn = parentCodeColumn,
            SourceDate = new DateOnly(2022, 5, 18),
        };

        private static RawTerritorialFeature Feature(string id, string? name = "R", string? parentCode = null, NetTopologySuite.Geometries.Geometry? geometry = null) =>
            new(id, name, id, parentCode, geometry ?? TestGeometry.Square(-58.5, -34.6, 0.001));

        [Fact]
        public async Task Falla_si_la_fuente_no_esta_registrada()
        {
            var source = Source();
            source.DataSourceCode = "desconocida";

            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(source).ImportAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Importa_en_lotes_y_cuenta_los_invalidos()
        {
            var source = Source();
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync(
                Feature("1"),
                Feature("2", name: null),
                Feature("3", geometry: TestGeometry.FromWkt("LINESTRING(0 0, 1 1)")),
                Feature("4", geometry: TestGeometry.FromWkt("POINT(1 1)", 22185)),
                Feature("5"),
                Feature("6")));

            var report = await Service(source).ImportAsync(CancellationToken.None);

            var result = Assert.Single(report.Sources);
            Assert.Equal("zm-radios", result.DataSourceCode);
            Assert.Equal("CensusTract", result.Target);
            Assert.Equal(6, result.Read);
            Assert.Equal(3, result.Imported);
            Assert.Equal(3, result.Invalid);
            Assert.Equal(0, result.Skipped);
            Assert.Equal([2, 1], _upserts.Select(batch => batch.Count));
            Assert.All(_upserts.SelectMany(batch => batch), unit => Assert.Equal(TestData.Now, unit.ImportedAt));
            await _units.DidNotReceiveWithAnyArgs().GetCodeIndexAsync(default, default);
        }

        [Fact]
        public async Task Resuelve_el_padre_por_codigo()
        {
            var source = Source(TerritorialUnitType.Partido, "depto");
            _units.GetCodeIndexAsync(TerritorialUnitType.Partido, Arg.Any<CancellationToken>())
                .Returns(new Dictionary<string, long> { ["427"] = 42 });
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync(
                Feature("1", parentCode: " 427 "),
                Feature("2", parentCode: "999"),
                Feature("3", parentCode: null)));

            var result = (await Service(source).ImportAsync(CancellationToken.None)).Sources[0];

            Assert.Equal(1, result.Imported);
            Assert.Equal(2, result.Skipped);
            Assert.Equal(42, _upserts.Single().Single().ParentId);
        }

        [Fact]
        public async Task Resuelve_el_padre_por_contencion_espacial()
        {
            var source = Source(TerritorialUnitType.Comuna);
            _units.FindContainingAsync(Arg.Any<IReadOnlyCollection<TerritorialUnitType>>(), Arg.Any<NetTopologySuite.Geometries.Point>(), Arg.Any<CancellationToken>())
                .Returns(new TerritorialUnitSummary(10, TerritorialUnitType.Comuna, "COMUNA 10", "Comuna 10", null), (TerritorialUnitSummary?)null);
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync(Feature("1"), Feature("2")));

            var result = (await Service(source).ImportAsync(CancellationToken.None)).Sources[0];

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(10, _upserts.Single().Single().ParentId);
        }

        [Fact]
        public async Task Fuente_vacia_no_guarda_nada()
        {
            var source = Source();
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync<RawTerritorialFeature>());

            var result = (await Service(source).ImportAsync(CancellationToken.None)).Sources[0];

            Assert.Equal(0, result.Read);
            Assert.Empty(_upserts);
        }
    }

    public class PoiImportServiceTests
    {
        private readonly IPoiSourceReader _reader = Substitute.For<IPoiSourceReader>();
        private readonly IPoiRepository _pois = Substitute.For<IPoiRepository>();
        private readonly IPoiCatalogRepository _catalog = Substitute.For<IPoiCatalogRepository>();
        private readonly IDataSourceRepository _dataSources = Substitute.For<IDataSourceRepository>();
        private readonly List<IReadOnlyList<PointOfInterest>> _upserts = [];

        public PoiImportServiceTests()
        {
            _dataSources.GetByCodeAsync("osm", Arg.Any<CancellationToken>())
                .Returns(TestData.WithId(new DataSource("osm", "OSM", DataSourceKind.Community, null, null, null), (short)5));
            _catalog.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(
                [TestData.WithId(new PoiCategory("transporte.parada_colectivo", "Parada de colectivo", 1, 3), (short)103)]);
            _catalog.GetRulesAsync(Arg.Any<CancellationToken>()).Returns([new PoiMappingRule(5, "highway", "bus_stop", 103, 10)]);
            _pois.UpsertAsync(Arg.Any<short>(), Arg.Do<IReadOnlyList<PointOfInterest>>(_upserts.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }

        private static PoiSourceOptions Source() => new()
        {
            DataSourceCode = "osm",
            Table = "public.planet_osm_point",
            IdPrefix = "n",
            OsmIdentifiers = true,
            OwnershipAttribute = "operator:type",
            PublicValues = ["public"],
            PrivateValues = ["private"],
        };

        [Theory]
        [InlineData("123", true, "n", "n123")]
        [InlineData(" -77 ", true, "w", "r77")]
        [InlineData("-77", false, "x-", "x--77")]
        public void BuildExternalId_respeta_los_ids_de_osm(string rawId, bool osm, string prefix, string expected)
        {
            var source = new PoiSourceOptions { OsmIdentifiers = osm, IdPrefix = prefix };

            Assert.Equal(expected, PoiImportService.BuildExternalId(rawId, source));
        }

        [Fact]
        public void ResolveOwnership_mapea_valores_publicos_y_privados()
        {
            var source = Source();

            Assert.Equal(Ownership.Unknown, PoiImportService.ResolveOwnership(new PoiSourceOptions(), new Dictionary<string, string> { ["x"] = "public" }));
            Assert.Equal(Ownership.Unknown, PoiImportService.ResolveOwnership(source, new Dictionary<string, string>()));
            Assert.Equal(Ownership.Public, PoiImportService.ResolveOwnership(source, new Dictionary<string, string> { ["operator:type"] = " Public " }));
            Assert.Equal(Ownership.Private, PoiImportService.ResolveOwnership(source, new Dictionary<string, string> { ["operator:type"] = "PRIVATE" }));
            Assert.Equal(Ownership.Unknown, PoiImportService.ResolveOwnership(source, new Dictionary<string, string> { ["operator:type"] = "community" }));
        }

        [Fact]
        public async Task Falla_si_la_fuente_no_esta_registrada()
        {
            var source = Source();
            source.DataSourceCode = "desconocida";
            var service = new PoiImportService(_reader, _pois, _catalog, _dataSources, TestData.Clock(),
                TestData.Options(options => options.PoiSources = [source]));

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(CancellationToken.None));
        }

        [Fact]
        public async Task Clasifica_normaliza_e_importa_en_lotes()
        {
            var source = Source();
            var stop = new Dictionary<string, string> { ["highway"] = "bus_stop", ["operator:type"] = "public" };
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync(
                new RawPoiFeature("1", null, "Rivadavia 9800", stop, TestGeometry.Point(-58.50, -34.63)),
                new RawPoiFeature("2", "Parking", null, new Dictionary<string, string> { ["amenity"] = "parking" }, TestGeometry.Point(-58.50, -34.63)),
                new RawPoiFeature("3", "Vacía", null, stop, TestGeometry.FromWkt("POINT EMPTY")),
                new RawPoiFeature("4", "Parada 2", null, stop, TestGeometry.Point(-58.51, -34.63)),
                new RawPoiFeature("-5", "Parada 3", null, stop, TestGeometry.Point(-58.52, -34.63))));
            var service = new PoiImportService(_reader, _pois, _catalog, _dataSources, TestData.Clock(),
                TestData.Options(options =>
                {
                    options.BatchSize = 2;
                    options.PoiSources = [source];
                }));

            var result = (await service.ImportAsync(CancellationToken.None)).Sources.Single();

            Assert.Equal(5, result.Read);
            Assert.Equal(3, result.Imported);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(1, result.Invalid);
            Assert.Equal([2, 1], _upserts.Select(batch => batch.Count));
            var first = _upserts[0][0];
            Assert.Equal("Parada de colectivo", first.Name);
            Assert.Equal("n1", first.ExternalId);
            Assert.Equal(Ownership.Public, first.Ownership);
            Assert.Equal("Rivadavia 9800", first.Address);
            Assert.Equal("r5", _upserts[1][0].ExternalId);
        }

        [Fact]
        public async Task Fuente_vacia_no_guarda_nada()
        {
            var source = Source();
            _reader.ReadAsync(source, Arg.Any<CancellationToken>()).Returns(TestData.ToAsync<RawPoiFeature>());
            var service = new PoiImportService(_reader, _pois, _catalog, _dataSources, TestData.Clock(),
                TestData.Options(options => options.PoiSources = [source]));

            var report = await service.ImportAsync(CancellationToken.None);

            Assert.Equal(0, report.Sources.Single().Read);
            Assert.Empty(_upserts);
        }
    }

    public class PoiDeduplicationServiceTests
    {
        private readonly IPoiRepository _pois = Substitute.For<IPoiRepository>();

        private static PoiMatchCandidate Candidate(long id, string name, double longitude, DataSourceKind kind) =>
            new(id, name, TestGeometry.Point(longitude, -34.65), kind);

        [Fact]
        public async Task Vincula_cada_poi_una_sola_vez_empezando_por_los_mas_cercanos()
        {
            var official = Candidate(1, "ESCUELA 39", -58.5000, DataSourceKind.Official);
            var osm = Candidate(2, "ESCUELA N 39", -58.5001, DataSourceKind.Community);
            var otherOsm = Candidate(3, "EP 39", -58.5003, DataSourceKind.Community);
            var otherOfficial = Candidate(4, "ESCUELA 39", -58.5004, DataSourceKind.Official);
            var unrelated = Candidate(5, "JARDIN LOS PITUFOS", -58.5002, DataSourceKind.Community);
            IReadOnlyList<PoiDuplicate>? marked = null;

            _pois.FindDuplicateCandidatesAsync(100, Arg.Any<CancellationToken>()).Returns(
            [
                new PoiDuplicateCandidate(official, otherOsm, 30),
                new PoiDuplicateCandidate(official, osm, 9),
                new PoiDuplicateCandidate(otherOfficial, osm, 25),
                new PoiDuplicateCandidate(otherOfficial, unrelated, 18),
            ]);
            await _pois.MarkDuplicatesAsync(Arg.Do<IReadOnlyList<PoiDuplicate>>(list => marked = list), Arg.Any<CancellationToken>());

            var service = new PoiDeduplicationService(_pois, TestData.Options());
            var report = await service.RunAsync(CancellationToken.None);

            Assert.Equal(4, report.Candidates);
            Assert.Equal(1, report.Duplicates);
            Assert.Equal(new PoiDuplicate(2, 1), Assert.Single(marked!));
        }

        [Fact]
        public async Task Sin_duplicados_no_escribe()
        {
            _pois.FindDuplicateCandidatesAsync(100, Arg.Any<CancellationToken>()).Returns([]);

            var report = await new PoiDeduplicationService(_pois, TestData.Options()).RunAsync(CancellationToken.None);

            Assert.Equal(new DeduplicationReport(0, 0), report);
            await _pois.DidNotReceiveWithAnyArgs().MarkDuplicatesAsync(default!, default);
        }
    }

    public class ZoneBuildServiceTests
    {
        private readonly ITerritorialUnitRepository _units = Substitute.For<ITerritorialUnitRepository>();
        private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();
        private IReadOnlyList<Zone> _synced = [];

        public ZoneBuildServiceTests()
        {
            _units.GetByTypesAsync(Arg.Is<IReadOnlyCollection<TerritorialUnitType>>(types => types.Contains(TerritorialUnitType.Partido)), Arg.Any<CancellationToken>())
                .Returns(
                [
                    new TerritorialUnitSummary(1, TerritorialUnitType.Partido, "427", "La Matanza", null),
                    new TerritorialUnitSummary(2, TerritorialUnitType.Partido, "638", "Pilar", null),
                    new TerritorialUnitSummary(3, TerritorialUnitType.Partido, null, "Sin código", null),
                ]);
            _units.GetByTypesAsync(Arg.Is<IReadOnlyCollection<TerritorialUnitType>>(types => types.Contains(TerritorialUnitType.Barrio)), Arg.Any<CancellationToken>())
                .Returns(
                [
                    new TerritorialUnitSummary(14, TerritorialUnitType.Localidad, null, "San Justo", 1),
                    new TerritorialUnitSummary(10, TerritorialUnitType.Barrio, null, "Villa Luro", 5),
                    new TerritorialUnitSummary(11, TerritorialUnitType.Localidad, null, "Ramos Mejía", 1),
                    new TerritorialUnitSummary(12, TerritorialUnitType.Localidad, null, "Del Viso", 2),
                    new TerritorialUnitSummary(13, TerritorialUnitType.Localidad, null, "San Justo", 1),
                    new TerritorialUnitSummary(15, TerritorialUnitType.Localidad, null, "Huérfana", null),
                    new TerritorialUnitSummary(16, TerritorialUnitType.Localidad, null, "Partido desconocido", 99),
                    new TerritorialUnitSummary(17, TerritorialUnitType.Localidad, null, "Sin código", 3),
                ]);
            _zones.SyncAsync(Arg.Do<IReadOnlyList<Zone>>(zones => _synced = zones), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            _zones.RebuildAdjacenciesAsync(Arg.Any<CancellationToken>()).Returns(12);
        }

        [Fact]
        public async Task Arma_zonas_de_barrios_y_localidades_en_alcance()
        {
            var service = new ZoneBuildService(_units, _zones, TestData.Options(options => options.ScopePartidoCodes = ["427"]));

            var report = await service.RebuildAsync(CancellationToken.None);

            Assert.Equal(new ZoneBuildReport(4, 12), report);
            Assert.Equal(
                ["villa-luro-caba", "ramos-mejia-la-matanza", "san-justo-la-matanza", "san-justo-la-matanza-2"],
                _synced.Select(zone => zone.Slug));
        }

        [Fact]
        public async Task Sin_alcance_configurado_acepta_cualquier_partido()
        {
            var service = new ZoneBuildService(_units, _zones, TestData.Options());

            await service.RebuildAsync(CancellationToken.None);

            Assert.Contains(_synced, zone => zone.Slug == "del-viso-pilar");
            Assert.Contains(_synced, zone => zone.Slug == "sin-codigo-sin-codigo");
            Assert.Equal(6, _synced.Count);
        }
    }
}
