using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Api.IntegrationTests.Support;

public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgis/postgis:15-3.3")
        .WithDatabase("zonamatch_integration_tests")
        .WithUsername("zonamatch_test")
        .WithPassword("zonamatch_integration_test_password")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public ZonaMatchDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ZonaMatchDbContext>();
        options.UseZonaMatchNpgsql(ConnectionString);
        return new ZonaMatchDbContext(options.Options);
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }
}
