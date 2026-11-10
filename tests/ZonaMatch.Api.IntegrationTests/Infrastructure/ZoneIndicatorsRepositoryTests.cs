using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

// Requires Docker: reads the district crime averages loaded by etl/calculate_zones.sql.
[Collection(ApiIntegrationCollection.Name)]
public sealed class ZoneIndicatorsRepositoryTests(PostgreSqlDatabaseFixture database) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = database.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS geo;
            CREATE TABLE IF NOT EXISTS geo.partido_delitos (
                partido text PRIMARY KEY, poblacion integer NOT NULL, delitos_comparables integer);

            DELETE FROM geo.partido_delitos WHERE partido LIKE 'Test %';
            INSERT INTO geo.partido_delitos (partido, poblacion, delitos_comparables) VALUES
                ('Test Norte', 100000, 5000),
                ('Test Sur', 300000, 9000);
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetDistrictCrimeRateAsync_WhenOneDistrict_ReturnsItsRate()
    {
        await using var context = database.CreateDbContext();

        var rate = await Repository(context).GetDistrictCrimeRateAsync(["Test Norte"]);

        rate.Should().Be(50.0);
    }

    [Fact]
    public async Task GetDistrictCrimeRateAsync_WhenTwoDistricts_WeighsByPopulation()
    {
        await using var context = database.CreateDbContext();

        var rate = await Repository(context).GetDistrictCrimeRateAsync(["Test Norte", "Test Sur"]);

        // (5,000 + 9,000) crimes / 400,000 residents
        rate.Should().Be(35.0);
    }

    [Fact]
    public async Task GetDistrictCrimeRateAsync_WhenDistrictIsUnknown_ReturnsNull()
    {
        await using var context = database.CreateDbContext();

        var rate = await Repository(context).GetDistrictCrimeRateAsync(["Test Inexistente"]);

        rate.Should().BeNull();
    }

    private static ZoneIndicatorsRepository Repository(ZonaMatch.Infrastructure.Data.ZonaMatchDbContext context) =>
        new(context, NullLogger<ZoneIndicatorsRepository>.Instance);
}
