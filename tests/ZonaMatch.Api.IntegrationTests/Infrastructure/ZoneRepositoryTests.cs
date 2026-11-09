using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

// Requires Docker: runs the zone queries against PostGIS with a small OSM import (CABA, Comuna 14, Palermo, Núñez).
[Collection(ApiIntegrationCollection.Name)]
public sealed partial class ZoneRepositoryTests(PostgreSqlDatabaseFixture database) : IAsyncLifetime
{
    // Boxes in WGS84 (west, south, east, north); each zone is drawn as its box.
    private static readonly (long OsmId, string Name, int Level, string Slug, double[] Box)[] Zones =
    [
        (-91001, "Ciudad Autónoma de Buenos Aires", 4, "ciudad-autonoma-de-buenos-aires", [-58.53, -34.71, -58.33, -34.53]),
        (-91002, "Comuna 14", 5, "comuna-14", [-58.44, -34.60, -58.39, -34.56]),
        (-91003, "Palermo", 10, "palermo", [-58.44, -34.60, -58.40, -34.565]),
        (-91004, "Núñez", 10, "nunez", [-58.48, -34.56, -58.45, -34.53])
    ];

    public async Task InitializeAsync()
    {
        await using var context = database.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS osm;
            CREATE SCHEMA IF NOT EXISTS geo;
            CREATE TABLE IF NOT EXISTS osm.planet_osm_polygon (
                osm_id bigint, name text, admin_level text, boundary text, way geometry(Geometry, 3857), way_area real);
            CREATE TABLE IF NOT EXISTS geo.zonas (
                osm_id bigint PRIMARY KEY, nombre text NOT NULL, slug text NOT NULL, nivel integer NOT NULL,
                superficie_km2 double precision NOT NULL, geom geometry(MultiPolygon, 4326) NOT NULL,
                elegida boolean NOT NULL DEFAULT false);
            """);

        // The slug function is taken from the ETL itself, so the search is tested with the rule that creates the slugs
        await context.Database.ExecuteSqlRawAsync(ReadSlugFunctionFromEtl());

        foreach (var (osmId, name, level, slug, box) in Zones)
        {
            var envelope = FormattableString.Invariant($"ST_MakeEnvelope({box[0]}, {box[1]}, {box[2]}, {box[3]}, 4326)");
            await context.Database.ExecuteSqlRawAsync($$"""
                INSERT INTO osm.planet_osm_polygon (osm_id, name, admin_level, boundary, way, way_area)
                SELECT {{osmId}}, @name, '{{level}}', 'administrative', way, ST_Area(way)
                FROM (SELECT ST_Transform({{envelope}}, 3857) AS way) w
                WHERE NOT EXISTS (SELECT 1 FROM osm.planet_osm_polygon WHERE osm_id = {{osmId}});
                INSERT INTO geo.zonas (osm_id, nombre, slug, nivel, superficie_km2, geom, elegida)
                VALUES ({{osmId}}, @name, '{{slug}}', {{level}}, ST_Area({{envelope}}::geography) / 1e6, ST_Multi({{envelope}}), {{level >= 5}})
                ON CONFLICT (osm_id) DO NOTHING;
                """, new NpgsqlParameter("name", name));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetBySlugAsync_WhenZoneExists_ReturnsZoneWithJurisdictionsAndBoundary()
    {
        await using var context = database.CreateDbContext();

        var zone = await new ZoneRepository(context).GetBySlugAsync("palermo", 0.00015);

        zone.Should().NotBeNull();
        zone!.OsmId.Should().Be(-91003);
        zone.FeatureId.Should().Be("r91003");
        zone.Name.Should().Be("Palermo");
        zone.AdministrativeLevel.Should().Be(10);
        zone.AreaKm2.Should().BeGreaterThan(0);
        zone.Jurisdictions.Should().Equal(
            new ZoneJurisdiction("Comuna 14", 5), new ZoneJurisdiction("Ciudad Autónoma de Buenos Aires", 4));
        zone.Boundary!.CenterLatitude.Should().BeInRange(-34.60, -34.565);
        zone.Boundary.CenterLongitude.Should().BeInRange(-58.44, -58.40);
        zone.Boundary.GeometryGeoJson.Should().Contain("\"type\":\"Polygon\"");
    }

    [Fact]
    public async Task GetBySlugAsync_WhenSlugDoesNotExist_ReturnsNull()
    {
        await using var context = database.CreateDbContext();

        var zone = await new ZoneRepository(context).GetBySlugAsync("no-existe", 0.00015);

        zone.Should().BeNull();
    }

    [Theory]
    [InlineData("NÚÑEZ")]
    [InlineData("Nuñez")]
    [InlineData("nu")]
    public async Task SearchAsync_WhenTextHasAccentsOrCapitals_FindsZoneWithoutBoundary(string searchText)
    {
        await using var context = database.CreateDbContext();

        var zones = await new ZoneRepository(context).SearchAsync(searchText, 10);

        var zone = zones.Should().ContainSingle(item => item.Slug == "nunez").Subject;
        zone.Name.Should().Be("Núñez");
        zone.Jurisdictions.Should().Equal(new ZoneJurisdiction("Ciudad Autónoma de Buenos Aires", 4));
        zone.Boundary.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_WhenTextHasLikeWildcards_DoesNotMatchEveryZone()
    {
        await using var context = database.CreateDbContext();

        var zones = await new ZoneRepository(context).SearchAsync("%_", 10);

        zones.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-34.58, -58.42, true)]
    [InlineData(-34.54, -58.46, false)]
    public async Task ContainsPointAsync_WhenPointIsChecked_ReturnsWhetherItIsInsideTheZone(double latitude, double longitude, bool expected)
    {
        await using var context = database.CreateDbContext();

        var contains = await new ZoneRepository(context).ContainsPointAsync("palermo", latitude, longitude);

        contains.Should().Be(expected);
    }

    [Theory]
    [InlineData("palermo", true)]
    [InlineData("ciudad-autonoma-de-buenos-aires", false)]
    public async Task ExistsAsync_WhenSlugIsChecked_ReturnsWhetherItIsAChosenZone(string slug, bool expected)
    {
        await using var context = database.CreateDbContext();

        var exists = await new ZoneRepository(context).ExistsAsync(slug);

        exists.Should().Be(expected);
    }

    private static string ReadSlugFunctionFromEtl()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "etl", "calculate_zones.sql")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException("etl/calculate_zones.sql was not found above the test output folder.");

        var etl = File.ReadAllText(Path.Combine(directory.FullName, "etl", "calculate_zones.sql"));
        return SlugFunction().Match(etl).Value;
    }

    [GeneratedRegex(@"CREATE OR REPLACE FUNCTION geo\.slug_zona.*?\$\$;", RegexOptions.Singleline)]
    private static partial Regex SlugFunction();
}
