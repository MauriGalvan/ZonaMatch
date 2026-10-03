using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Repositories;
using ZonaMatch.Infrastructure.Routing;
using ZonaMatch.Infrastructure.Sources;

namespace ZonaMatch.Infrastructure
{
    public static class DependencyInjection
    {
        public const string ConnectionStringName = "DefaultConnection";

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Falta la cadena de conexión '{ConnectionStringName}'.");

            // un único data source con NetTopologySuite: lo comparten EF Core y los lectores de fuentes
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseNetTopologySuite();
            services.AddSingleton(dataSourceBuilder.Build());

            services.AddDbContext<ZonaMatchDbContext>((provider, options) =>
                options.UseNpgsql(
                    provider.GetRequiredService<NpgsqlDataSource>(),
                    npgsql => npgsql
                        .UseNetTopologySuite()
                        // el historial queda donde lo dejó InitialCreate, aunque el modelo use otro esquema
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, "public")));

            services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));

            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IDataSourceRepository, DataSourceRepository>();
            services.AddScoped<ITerritorialUnitRepository, TerritorialUnitRepository>();
            services.AddScoped<IZoneRepository, ZoneRepository>();
            services.AddScoped<IIndicatorRepository, IndicatorRepository>();
            services.AddScoped<IPoiCatalogRepository, PoiCatalogRepository>();
            services.AddScoped<IPoiRepository, PoiRepository>();

            services.AddScoped<ITerritorialSourceReader, PostgisTerritorialSourceReader>();
            services.AddScoped<IPoiSourceReader, PostgisPoiSourceReader>();
            services.AddSingleton<IRoutingService, EstimatedRoutingService>();

            return services;
        }
    }
}
