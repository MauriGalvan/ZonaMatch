using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

// Requires Docker: clips the transit line paths against a zone in PostGIS.
[Collection(ApiIntegrationCollection.Name)]
public sealed class ZoneTransitRepositoryTests(PostgreSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string Slug = "zona-transporte";

    public async Task InitializeAsync()
    {
        await using var context = database.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS geo;
            CREATE TABLE IF NOT EXISTS geo.zonas (
                osm_id bigint PRIMARY KEY, nombre text NOT NULL, slug text NOT NULL, nivel integer NOT NULL,
                superficie_km2 double precision NOT NULL, geom geometry(MultiPolygon, 4326) NOT NULL,
                elegida boolean NOT NULL DEFAULT false);
            CREATE TABLE IF NOT EXISTS geo.transporte_recorridos (
                modo text NOT NULL, linea text NOT NULL, recorrido_id text NOT NULL, geom geometry(LineString, 4326) NOT NULL);

            INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom, elegida)
            VALUES (-92001, 'Zona Transporte', 'zona-transporte', 10, 1,
                    ST_Multi(ST_MakeEnvelope(-58.50, -34.60, -58.40, -34.50, 4326)), true)
            ON CONFLICT (osm_id) DO NOTHING;

            DELETE FROM geo.transporte_recorridos WHERE recorrido_id LIKE 'test-%';
            INSERT INTO geo.transporte_recorridos (modo, linea, recorrido_id, geom) VALUES
                -- Crosses the zone from west to east: only the inside part is kept
                ('colectivo', '110', 'test-110-ida', ST_GeomFromText('LINESTRING(-58.60 -34.55, -58.30 -34.55)', 4326)),
                -- Two shapes of the same line become one route
                ('colectivo', '110', 'test-110-vuelta', ST_GeomFromText('LINESTRING(-58.45 -34.65, -58.45 -34.45)', 4326)),
                -- Touches the boundary at a single corner: no line inside
                ('tren', 'Mitre', 'test-mitre', ST_GeomFromText('LINESTRING(-58.40 -34.50, -58.30 -34.40)', 4326)),
                -- Outside the zone
                ('subte', 'D', 'test-d', ST_GeomFromText('LINESTRING(-58.20 -34.30, -58.10 -34.20)', 4326));
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListRoutesAsync_WhenLinesCrossTheZone_ReturnsOneClippedRoutePerLine()
    {
        await using var context = database.CreateDbContext();

        var routes = await new ZoneTransitRepository(context, NullLogger<ZoneTransitRepository>.Instance).ListRoutesAsync(Slug);

        var route = routes.Should().ContainSingle().Subject;
        route.Mode.Should().Be("colectivo");
        route.Line.Should().Be("110");
        route.GeometryGeoJson.Should().Contain("MultiLineString").And.Contain("-58.5").And.NotContain("-58.6");
    }

    [Fact]
    public async Task ListRoutesAsync_WhenZoneDoesNotExist_ReturnsEmptyList()
    {
        await using var context = database.CreateDbContext();

        var routes = await new ZoneTransitRepository(context, NullLogger<ZoneTransitRepository>.Instance).ListRoutesAsync("no-existe");

        routes.Should().BeEmpty();
    }
}
