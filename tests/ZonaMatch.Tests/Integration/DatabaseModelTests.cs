using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Tests.Integration
{
    // PBI 3 · tarea 3.4: el modelo se instancia en una base vacía sin errores
    [Collection(PostgisCollection.Name)]
    public class DatabaseModelTests
    {
        private readonly PostgisFixture _fixture;

        public DatabaseModelTests(PostgisFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Las_migraciones_crean_el_esquema_canonico_con_sus_indices_espaciales()
        {
            var tables = await _fixture.ScalarAsync<long>(
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'zonamatch'");
            var gistIndexes = await _fixture.ScalarAsync<long>(
                "SELECT count(*) FROM pg_indexes WHERE schemaname = 'zonamatch' AND indexdef ILIKE '%USING gist%'");
            var geometryType = await _fixture.ScalarAsync<string>(
                "SELECT type FROM geometry_columns WHERE f_table_schema = 'zonamatch' AND f_table_name = 'territorial_units'");
            var srid = await _fixture.ScalarAsync<int>(
                "SELECT srid FROM geometry_columns WHERE f_table_schema = 'zonamatch' AND f_table_name = 'points_of_interest' AND f_geometry_column = 'location'");

            Assert.Equal(8, tables);
            Assert.Equal(3, gistIndexes);
            Assert.Equal("MULTIPOLYGON", geometryType);
            Assert.Equal(4326, srid);
        }

        [Fact]
        public async Task El_catalogo_inicial_queda_cargado()
        {
            var counts = await _fixture.WithDbContextAsync(async dbContext => (
                Sources: await dbContext.DataSources.CountAsync(),
                Categories: await dbContext.PoiCategories.CountAsync(),
                Roots: await dbContext.PoiCategories.CountAsync(category => category.ParentId == null),
                Rules: await dbContext.PoiMappingRules.CountAsync()));

            Assert.Equal(7, counts.Sources);
            Assert.Equal(33, counts.Categories);
            Assert.Equal(7, counts.Roots);
            Assert.Equal(33, counts.Rules);
        }

        [Fact]
        public async Task Las_migraciones_suben_y_bajan_en_una_base_nueva()
        {
            await _fixture.ExecuteAsync("CREATE DATABASE migration_check");
            var connectionString = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = "migration_check" }.ToString();
            await using var factory = _fixture.CreateFactory(ingestionEnabled: false, connectionString: connectionString);
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ZonaMatchDbContext>();

            await dbContext.Database.MigrateAsync();
            var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).Count();
            await Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(dbContext).MigrateAsync("0");
            var afterDown = (await dbContext.Database.GetAppliedMigrationsAsync()).Count();
            await dbContext.Database.MigrateAsync();

            Assert.Equal(2, applied);
            Assert.Equal(0, afterDown);
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        }

        [Fact]
        public void Los_atributos_jsonb_se_comparan_por_contenido()
        {
            var comparer = _fixture.WithDbContextAsync(dbContext => Task.FromResult(dbContext.Model
                .FindEntityType(typeof(PointOfInterest))!
                .FindProperty(nameof(PointOfInterest.Attributes))!
                .GetValueComparer())).GetAwaiter().GetResult();

            var original = new Dictionary<string, string> { ["nivel"] = "Nivel Primario" };
            var snapshot = (Dictionary<string, string>)comparer.Snapshot(original)!;

            Assert.NotSame(original, snapshot);
            Assert.True(comparer.Equals(original, snapshot));
            Assert.False(comparer.Equals(original, new Dictionary<string, string> { ["nivel"] = "Nivel Inicial" }));
            Assert.False(comparer.Equals(original, new Dictionary<string, string>()));
            Assert.Equal(comparer.GetHashCode(original), comparer.GetHashCode(snapshot));
        }

        [Fact]
        public async Task El_repositorio_generico_hace_el_crud_completo()
        {
            await using var scope = _fixture.Factory.Services.CreateAsyncScope();
            // el repositorio genérico busca por clave int: se prueba con las reglas de mapeo
            var repository = new Repository<PoiMappingRule>(scope.ServiceProvider.GetRequiredService<ZonaMatchDbContext>());

            var created = await repository.AddAsync(new PoiMappingRule(5, "amenity", "veterinary", 402, 10));
            Assert.True(created.Id >= 1000);
            Assert.Same(created, await repository.GetByIdAsync(created.Id));
            Assert.Contains(await repository.GetAllAsync(), rule => rule.Value == "veterinary");

            await repository.UpdateAsync(created);
            await repository.DeleteAsync(created);

            Assert.DoesNotContain(await repository.GetAllAsync(), rule => rule.Value == "veterinary");
        }
    }
}
