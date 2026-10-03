using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Tests.Integration
{
    [CollectionDefinition(Name)]
    public class PostgisCollection : ICollectionFixture<PostgisFixture>
    {
        public const string Name = "postgis";
    }

    // levanta PostGIS en Docker, migra una base vacía, crea las tablas de origen y corre la ingesta completa por la API
    public sealed class PostgisFixture : IAsyncLifetime
    {
        public const string Image = "postgis/postgis:15-3.3";

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
            .WithDatabase("zonamatch")
            .WithUsername("zonamatch")
            .WithPassword("zonamatch")
            .Build();

        public string ConnectionString => _container.GetConnectionString();

        public WebApplicationFactory<Program> Factory { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;

        public JsonElement TerritorialReport { get; private set; }
        public JsonElement ZoneReport { get; private set; }
        public JsonElement PoiReport { get; private set; }
        public JsonElement DeduplicationReport { get; private set; }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            Factory = CreateFactory(ingestionEnabled: true);
            Client = Factory.CreateClient();

            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ZonaMatchDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            await ExecuteAsync(SourceFixtures.Sql);

            TerritorialReport = await PostAsync("/api/admin/ingestion/territorial");
            await SeedIndicatorsAsync();
            ZoneReport = await PostAsync("/api/admin/ingestion/zones");
            PoiReport = await PostAsync("/api/admin/ingestion/pois");
            DeduplicationReport = await PostAsync("/api/admin/ingestion/deduplicate");
        }

        public async Task DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            await _container.DisposeAsync();
        }

        public WebApplicationFactory<Program> CreateFactory(bool ingestionEnabled, string environment = "Development", string? connectionString = null)
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString ?? ConnectionString);
                builder.UseSetting("Admin:IngestionEndpointsEnabled", ingestionEnabled.ToString());
            });
        }

        public async Task<JsonElement> PostAsync(string path)
        {
            var response = await Client.PostAsync(path, null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async Task<TResult> WithDbContextAsync<TResult>(Func<ZonaMatchDbContext, Task<TResult>> action)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<ZonaMatchDbContext>());
        }

        // indicadores con distinta procedencia para probar herencia y trazabilidad
        private async Task SeedIndicatorsAsync()
        {
            await WithDbContextAsync(async dbContext =>
            {
                var units = await dbContext.TerritorialUnits
                    .Where(unit => unit.Name == "La Matanza" || unit.Name == "Villa Luro" || unit.Name == "Ramos Mejía")
                    .ToDictionaryAsync(unit => unit.Name);
                var snic = (await dbContext.DataSources.SingleAsync(source => source.Code == "zm-partidos")).Id;
                var indec = (await dbContext.DataSources.SingleAsync(source => source.Code == "zm-radios")).Id;

                dbContext.TerritorialIndicators.AddRange(
                    new TerritorialIndicator(units["La Matanza"].Id, IndicatorCodes.CrimeRatePer1000, 45, "hechos/1000 hab", snic, new DateOnly(2025, 1, 1)),
                    new TerritorialIndicator(units["Villa Luro"].Id, IndicatorCodes.CrimeRatePer1000, 27, "hechos/1000 hab", snic, new DateOnly(2025, 1, 1)),
                    new TerritorialIndicator(units["Villa Luro"].Id, IndicatorCodes.Population, 34000, "hab", indec, new DateOnly(2022, 5, 18)),
                    new TerritorialIndicator(units["Ramos Mejía"].Id, IndicatorCodes.AverageRent, 650000, "ARS", indec, new DateOnly(2026, 9, 1)));

                return await dbContext.SaveChangesAsync();
            });
        }
    }
}
