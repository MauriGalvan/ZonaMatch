using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Tests.Integration
{
    // la configuración de appsettings.json corrida contra tablas con el formato real de cada fuente
    [Collection(PostgisCollection.Name)]
    public class IngestionPipelineTests
    {
        private readonly PostgisFixture _fixture;

        public IngestionPipelineTests(PostgisFixture fixture)
        {
            _fixture = fixture;
        }

        private static (int Read, int Imported, int Invalid, int Skipped) Counts(JsonElement source) => (
            source.GetProperty("read").GetInt32(),
            source.GetProperty("imported").GetInt32(),
            source.GetProperty("invalid").GetInt32(),
            source.GetProperty("skipped").GetInt32());

        [Fact]
        public void La_ingesta_territorial_normaliza_todas_las_fuentes()
        {
            var sources = _fixture.TerritorialReport.GetProperty("sources").EnumerateArray().ToList();

            Assert.Equal((1, 1, 0, 0), Counts(sources[0]));   // comunas
            Assert.Equal((4, 4, 0, 0), Counts(sources[1]));   // partidos (Tigre + islas agrupados)
            Assert.Equal((2, 1, 1, 0), Counts(sources[2]));   // radios CABA (uno vacío)
            Assert.Equal((2, 1, 0, 1), Counts(sources[3]));   // radios PBA (uno sin partido)
            Assert.Equal((2, 1, 0, 1), Counts(sources[4]));   // barrios OSM (uno fuera de CABA)
            Assert.Equal((6, 5, 0, 1), Counts(sources[5]));   // localidades OSM (una sin partido)
        }

        [Fact]
        public async Task Las_unidades_quedan_en_4326_con_jerarquia_y_nombre_de_la_parte_preferida()
        {
            var units = await _fixture.WithDbContextAsync(dbContext => dbContext.TerritorialUnits.AsNoTracking().ToListAsync());

            Assert.All(units, unit => Assert.Equal(4326, unit.Geometry.SRID));
            var tigre = units.Single(unit => unit.Type == TerritorialUnitType.Partido && unit.Code == "805");
            Assert.Equal("Tigre", tigre.Name);
            // continente e islas comparten borde: quedan disueltos en una sola superficie
            Assert.Equal(1, tigre.Geometry.NumGeometries);
            Assert.Equal(-34.45, tigre.Geometry.EnvelopeInternal.MinY, 6);
            Assert.Equal(-34.30, tigre.Geometry.EnvelopeInternal.MaxY, 6);

            var luro = units.Single(unit => unit.Name == "Villa Luro");
            Assert.Equal("-1001", luro.ExternalId);
            Assert.Equal(units.Single(unit => unit.Code == "COMUNA 10").Id, luro.ParentId);
            Assert.InRange(luro.Geometry.EnvelopeInternal.MinX, -58.5301, -58.5299);

            var radio = units.Single(unit => unit.Code == "064270101");
            Assert.Equal(units.Single(unit => unit.Code == "427").Id, radio.ParentId);
            Assert.Equal(new DateOnly(2022, 5, 18), radio.SourceDate);
        }

        [Fact]
        public void Las_zonas_son_barrios_y_localidades_de_los_partidos_en_alcance()
        {
            Assert.Equal(5, _fixture.ZoneReport.GetProperty("zones").GetInt32());
            // Ramos Mejía <-> Haedo y Ramos Mejía <-> Villa Luro, en ambos sentidos
            Assert.Equal(4, _fixture.ZoneReport.GetProperty("adjacencies").GetInt32());
        }

        [Fact]
        public async Task Padron_y_osm_terminan_en_el_mismo_formato()
        {
            var sources = _fixture.PoiReport.GetProperty("sources").EnumerateArray().ToList();
            Assert.Equal((3, 2, 0, 1), Counts(sources[0]));   // padrón: formación profesional sin regla
            Assert.Equal((6, 5, 0, 1), Counts(sources[1]));   // puntos OSM: estacionamiento sin regla
            Assert.Equal((1, 1, 0, 0), Counts(sources[2]));   // polígonos OSM

            var pois = await _fixture.WithDbContextAsync(dbContext => dbContext.PointsOfInterest
                .AsNoTracking()
                .Include(poi => poi.Category)
                .ToDictionaryAsync(poi => poi.ExternalId));

            var padron = pois["e1"];
            Assert.Equal("educacion.primaria", padron.Category!.Code);
            Assert.Equal(Ownership.Public, padron.Ownership);
            Assert.Equal("Av. de Mayo 700", padron.Address);
            Assert.Equal("300", padron.Attributes["matricula"]);

            var osmSchool = pois["n5004"];
            Assert.Equal("educacion.escuela", osmSchool.Category!.Code);
            Assert.Equal(4326, osmSchool.Location.SRID);
            Assert.InRange(osmSchool.Location.X, -58.56501, -58.56499);

            var busStop = pois["n5001"];
            Assert.Equal("Parada de colectivo", busStop.Name);
            Assert.Equal("Rivadavia 9800", busStop.Address);
            Assert.Equal(Ownership.Public, busStop.Ownership);

            Assert.Equal("transporte.subte", pois["n5003"].Category!.Code);
            Assert.Equal(Ownership.Unknown, pois["n5002"].Ownership);
            Assert.False(pois["n5002"].Attributes.ContainsKey("name:es"));
            Assert.Equal(Ownership.Public, pois["n5006"].Ownership);

            var park = pois["w4001"];
            Assert.Equal("espacios_verdes.plaza_parque", park.Category!.Code);
            Assert.NotNull(park.Footprint);
            Assert.True(park.Footprint!.Contains(park.Location));
        }

        [Fact]
        public async Task La_deduplicacion_deja_al_padron_como_canonico()
        {
            Assert.Equal(1, _fixture.DeduplicationReport.GetProperty("duplicates").GetInt32());

            var (padronId, osmCanonical) = await _fixture.WithDbContextAsync(async dbContext => (
                (await dbContext.PointsOfInterest.SingleAsync(poi => poi.ExternalId == "e1")).Id,
                (await dbContext.PointsOfInterest.SingleAsync(poi => poi.ExternalId == "n5004")).CanonicalPoiId));

            Assert.Equal(padronId, osmCanonical);
        }

        [Fact]
        public async Task Reimportar_es_idempotente()
        {
            var before = await _fixture.WithDbContextAsync(async dbContext => (
                await dbContext.TerritorialUnits.CountAsync(), await dbContext.PointsOfInterest.CountAsync(), await dbContext.Zones.CountAsync()));

            await _fixture.PostAsync("/api/admin/ingestion/territorial");
            await _fixture.PostAsync("/api/admin/ingestion/pois");
            await _fixture.PostAsync("/api/admin/ingestion/zones");
            var dedup = await _fixture.PostAsync("/api/admin/ingestion/deduplicate");

            var after = await _fixture.WithDbContextAsync(async dbContext => (
                await dbContext.TerritorialUnits.CountAsync(), await dbContext.PointsOfInterest.CountAsync(), await dbContext.Zones.CountAsync()));

            Assert.Equal(before, after);
            Assert.Equal(0, dedup.GetProperty("duplicates").GetInt32());
        }

        [Fact]
        public async Task Sincronizar_zonas_borra_las_que_ya_no_aplican()
        {
            await _fixture.WithDbContextAsync(async dbContext =>
            {
                var unit = await dbContext.TerritorialUnits.SingleAsync(item => item.Name == "Del Viso");
                dbContext.Zones.Add(new Zone(unit.Id, "del-viso-pilar", "Del Viso", "Pilar"));
                return await dbContext.SaveChangesAsync();
            });

            await _fixture.PostAsync("/api/admin/ingestion/zones");

            var exists = await _fixture.WithDbContextAsync(dbContext => dbContext.Zones.AnyAsync(zone => zone.Slug == "del-viso-pilar"));
            Assert.False(exists);
        }
    }
}
