using NetTopologySuite.Geometries;
using NSubstitute;
using ZonaMatch.Application.Analysis;
using ZonaMatch.Application.Common;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Territory;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Tests.Support;

namespace ZonaMatch.Tests.Application
{
    public class AnalysisServiceTests
    {
        private static readonly ZoneData Ramos = TestData.Zone(1, "ramos", -58.56, unitId: 10, parentUnitId: 100, name: "Ramos Mejía");
        private static readonly ZoneData Luro = TestData.Zone(2, "luro", -58.50, unitId: 20, parentUnitId: 200, name: "Villa Luro", parentName: "CABA", parentUnitName: "Comuna 10");
        private static readonly ZoneData Haedo = TestData.Zone(3, "haedo", -58.60, unitId: 30, parentUnitId: 300, name: "Haedo", parentName: "Morón", parentUnitName: "Morón");

        private readonly IZoneRepository _zones = Substitute.For<IZoneRepository>();
        private readonly IPoiRepository _pois = Substitute.For<IPoiRepository>();
        private readonly IIndicatorRepository _indicators = Substitute.For<IIndicatorRepository>();
        private readonly IRoutingService _routing = Substitute.For<IRoutingService>();
        private readonly AnalysisService _service;

        public AnalysisServiceTests()
        {
            _service = new AnalysisService(_zones, _pois, new IndicatorService(_indicators), _routing);

            _zones.GetBySlugsAsync(default!, default).ReturnsForAnyArgs(call =>
            {
                var slugs = call.Arg<IReadOnlyCollection<string>>();
                return new[] { Ramos, Luro, Haedo }.Where(zone => slugs.Contains(zone.Slug)).ToList();
            });
            _zones.GetNeighborsAsync(1, Arg.Any<CancellationToken>()).Returns(
            [
                new ZoneSummary(2, "luro", "Villa Luro", "CABA", TerritorialUnitType.Barrio),
                new ZoneSummary(3, "haedo", "Haedo", "Morón", TerritorialUnitType.Localidad),
            ]);
            _zones.GetNeighborsAsync(2, Arg.Any<CancellationToken>()).Returns(
                [new ZoneSummary(3, "haedo", "Haedo", "Morón", TerritorialUnitType.Localidad)]);

            _indicators.GetAsync(default!, default!, default).ReturnsForAnyArgs(
            [
                new IndicatorData(100, IndicatorCodes.CrimeRatePer1000, 45, "c/1000", "SNIC", new DateOnly(2025, 1, 1)),
                new IndicatorData(20, IndicatorCodes.CrimeRatePer1000, 27, "c/1000", "SNIC", new DateOnly(2025, 1, 1)),
                new IndicatorData(10, IndicatorCodes.AverageRent, 700_000, "ARS", "Argenprop", new DateOnly(2026, 9, 1)),
                new IndicatorData(30, IndicatorCodes.AverageRent, 500_000, "ARS", "Argenprop", new DateOnly(2026, 9, 1)),
            ]);

            _pois.CountWithinAsync(default!, default, default).ReturnsForAnyArgs(call =>
            {
                var x = Math.Round(call.Arg<Point>().X, 2);
                var (education, transport) = x switch
                {
                    -58.56 => (10, 5),
                    -58.50 => (20, 8),
                    _ => (30, 9),
                };
                return
                [
                    new PoiCategoryCount("educacion", "educacion.jardin", education - 1),
                    new PoiCategoryCount("educacion", "educacion.primaria", 1),
                    new PoiCategoryCount("transporte", "transporte.parada_colectivo", transport),
                ];
            });

            // Ramos: 10 min, Luro: 70 min, Haedo: 50 min
            _routing.EstimateAsync(default!, default!, default, default).ReturnsForAnyArgs(call =>
                new RouteEstimate(Math.Round(Math.Abs(call.ArgAt<Point>(0).X + 58.56) * 1000 + 10, 1), "estimado"));
        }

        private static ImportantPointRequest Point(string name, int? maxMinutes, int trips = 3, double latitude = -34.67, double longitude = -58.56) =>
            new(name, latitude, longitude, TravelMode.PublicTransport, trips, maxMinutes);

        private static ParticipantRequest Participant(
            string name,
            IReadOnlyList<CriterionRequest> criteria,
            IReadOnlyList<ImportantPointRequest>? points = null,
            double weight = 1) => new(name, weight, criteria, points ?? []);

        private static RankingRequest Request(
            IReadOnlyList<string> slugs,
            IReadOnlyList<ParticipantRequest> participants,
            double? maxRent = null,
            bool suggestion = false,
            int radius = 1000) => new(slugs, participants, radius, maxRent, suggestion);

        [Fact]
        public void GetCriteria_lista_el_catalogo()
        {
            var criteria = _service.GetCriteria();

            Assert.Equal(10, criteria.Count);
            Assert.Equal(new CriterionInfoDto("movilidad", "Movilidad"), criteria[0]);
        }

        [Fact]
        public void CriterionCatalog_rechaza_codigos_desconocidos()
        {
            Assert.Equal("Seguridad", CriterionCatalog.Get("seguridad").Name);
            Assert.Throws<ArgumentException>(() => CriterionCatalog.Get("playa"));
        }

        [Theory]
        [InlineData(RestrictionStatus.Complies, "cumple")]
        [InlineData(RestrictionStatus.NearLimit, "cerca_del_limite")]
        [InlineData(RestrictionStatus.Exceeds, "excede")]
        [InlineData(RestrictionStatus.Unknown, "sin_dato")]
        public void StatusName_por_estado(RestrictionStatus status, string expected)
        {
            Assert.Equal(expected, AnalysisService.StatusName(status));
        }

        public static TheoryData<RankingRequest> InvalidRequests()
        {
            var ok = new CriterionRequest("educacion", Priority.Medium);
            var participant = Participant("A", [ok]);

            return
            [
                Request([], [participant]),
                Request(Enumerable.Range(0, 21).Select(index => $"z{index}").ToList(), [participant]),
                Request(["ramos"], []),
                Request(["ramos"], Enumerable.Range(0, 6).Select(index => participant).ToList()),
                Request(["ramos"], [participant], radius: 50),
                Request(["ramos"], [participant], maxRent: 0),
                Request(["ramos"], [Participant("A", [ok], weight: 0)]),
                Request(["ramos"], [Participant("A", [])]),
                Request(["ramos"], [Participant("A", [ok], Enumerable.Range(0, 11).Select(index => Point($"p{index}", null)).ToList())]),
                Request(["ramos"], [Participant("A", [new CriterionRequest("playa", Priority.High)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null, latitude: 91)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null, longitude: -181)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null, trips: -1)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null, trips: 15)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", 0)])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null) with { Importance = 0 }])]),
                Request(["ramos"], [Participant("A", [ok], [Point("p", null) with { Importance = 5 }])]),
            ];
        }

        [Theory]
        [MemberData(nameof(InvalidRequests))]
        public async Task RankAsync_valida_la_solicitud(RankingRequest request)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.RankAsync(request, CancellationToken.None));
        }

        [Fact]
        public void WeightedMinutes_pondera_por_frecuencia_e_importancia()
        {
            var daily = Point("Oficina", null, trips: 5) with { Importance = 4 };
            var weekly = Point("Club", null, trips: 1) with { Importance = 1 };
            var occasional = Point("Familia", null, trips: 0);

            // (20*20 + 60*1) / 21
            Assert.Equal(21.9d, AnalysisService.WeightedMinutes([(daily, 20), (weekly, 60)]));
            // sin viajes declarados cuenta como uno por semana
            Assert.Equal(30d, AnalysisService.WeightedMinutes([(occasional, 30)]));
        }

        [Fact]
        public async Task RankAsync_informa_zonas_inexistentes()
        {
            var request = Request(["ramos", "no-existe"], [Participant("A", [new CriterionRequest("educacion", Priority.Low)])]);

            var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.RankAsync(request, CancellationToken.None));
            Assert.Equal("no-existe", exception.Key);
        }

        [Fact]
        public async Task RankAsync_combina_perfiles_restricciones_y_sugerencia()
        {
            var nicolas = Participant("Nicolás",
            [
                new CriterionRequest("movilidad", Priority.High),
                new CriterionRequest("seguridad", Priority.Medium),
                new CriterionRequest("educacion", Priority.Low),
                new CriterionRequest("educacion", Priority.High),
            ],
            [Point("UNLaM", 60), Point("Oficina", null, trips: 5)]);
            var lucia = Participant("Lucía",
            [
                new CriterionRequest("movilidad", Priority.Medium),
                new CriterionRequest("conectividad", Priority.High),
            ]);

            var result = await _service.RankAsync(
                Request(["ramos", "luro", "ramos"], [nicolas, lucia], maxRent: 720_000, suggestion: true),
                CancellationToken.None);

            Assert.Equal(1000, result.RadiusMeters);
            Assert.Equal(["luro", "ramos"], result.Zones.Select(zone => zone.Slug));
            Assert.Equal([1, 2], result.Zones.Select(zone => zone.Position));

            var luro = result.Zones[0];
            Assert.Equal(75d, luro.Score);
            Assert.Equal("Villa Luro", luro.Name);
            Assert.Equal("CABA", luro.ParentName);
            Assert.Equal(0.7d, luro.Coverage);
            Assert.Equal(
            [
                new CriterionResultDto("movilidad", "Movilidad", 62.5d, "tiempo estimado a tus puntos"),
                new CriterionResultDto("seguridad", "Seguridad", 100d, "directo · SNIC (2025)"),
                new CriterionResultDto("educacion", "Educación", 70d, "lugares en 1000 m"),
                new CriterionResultDto("conectividad", "Conectividad", null, "sin dato"),
            ], luro.Breakdown);
            Assert.Equal(
            [
                new ParticipantResultDto("Nicolás", 65d, 1120d),
                new ParticipantResultDto("Lucía", 85d, 0d),
            ], luro.Participants);
            Assert.Equal(
            [
                new RestrictionResultDto("max_rent", "Alquiler", null, "sin_dato", null, 720_000),
                new RestrictionResultDto("max_travel_time", "Tiempo a UNLaM", "Nicolás", "excede", 70d, 60),
            ], luro.Restrictions);

            var ramos = result.Zones[1];
            Assert.Equal(55d, ramos.Score);
            Assert.Equal("dato de La Matanza · SNIC (2025)", ramos.Breakdown[1].DataNote);
            Assert.Equal("cerca_del_limite", ramos.Restrictions[0].Status);
            Assert.Equal("cumple", ramos.Restrictions[1].Status);

            Assert.NotNull(result.Suggestion);
            Assert.Equal("haedo", result.Suggestion.Slug);
            Assert.Equal(0, result.Suggestion.Position);
            Assert.Equal(85d, result.Suggestion.Score);
        }

        [Fact]
        public async Task RankAsync_solo_con_indicadores_no_cuenta_pois_ni_sugiere()
        {
            var result = await _service.RankAsync(
                Request(["ramos", "luro"], [Participant("A", [new CriterionRequest("seguridad", Priority.High)])]),
                CancellationToken.None);

            Assert.Equal("luro", result.Zones[0].Slug);
            Assert.Null(result.Suggestion);
            await _pois.DidNotReceiveWithAnyArgs().CountWithinAsync(default!, default, default);
            await _zones.DidNotReceiveWithAnyArgs().GetNeighborsAsync(default, default);
        }

        [Fact]
        public async Task RankAsync_movilidad_sin_puntos_usa_la_oferta_de_transporte()
        {
            var result = await _service.RankAsync(
                Request(["ramos", "luro"], [Participant("A", [new CriterionRequest("movilidad", Priority.High)])]),
                CancellationToken.None);

            Assert.Equal("transporte en 1000 m", result.Zones[0].Breakdown[0].DataNote);
            Assert.Equal("luro", result.Zones[0].Slug);
        }

        [Fact]
        public async Task RankAsync_sin_aledanas_nuevas_no_busca_candidatas()
        {
            _zones.GetNeighborsAsync(1, Arg.Any<CancellationToken>()).Returns(
                [new ZoneSummary(2, "luro", "Villa Luro", "CABA", TerritorialUnitType.Barrio)]);
            _zones.GetNeighborsAsync(2, Arg.Any<CancellationToken>()).Returns([]);

            var result = await _service.RankAsync(
                Request(["ramos", "luro"], [Participant("A", [new CriterionRequest("educacion", Priority.High)])], suggestion: true),
                CancellationToken.None);

            Assert.Null(result.Suggestion);
            await _zones.ReceivedWithAnyArgs(1).GetBySlugsAsync(default!, default);
        }

        [Fact]
        public async Task RankAsync_no_sugiere_zonas_peores_que_las_elegidas()
        {
            _zones.GetNeighborsAsync(2, Arg.Any<CancellationToken>()).Returns(
                [new ZoneSummary(1, "ramos", "Ramos Mejía", "La Matanza", TerritorialUnitType.Localidad)]);

            var result = await _service.RankAsync(
                Request(["luro"], [Participant("A", [new CriterionRequest("educacion", Priority.High)])], suggestion: true),
                CancellationToken.None);

            Assert.Equal(100d, Assert.Single(result.Zones).Score);
            Assert.Null(result.Suggestion);
        }
    }
}
