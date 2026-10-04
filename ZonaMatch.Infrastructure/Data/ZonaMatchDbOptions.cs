using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ZonaMatch.Infrastructure.Data
{
    public static class ZonaMatchDbOptions
    {
        public const string AppSchema = "app";
        public const string GeoSchema = "geo";
        public const string OsmSchema = "osm";

        public static DbContextOptionsBuilder UseZonaMatchNpgsql(
            this DbContextOptionsBuilder options, string? connectionString)
            => options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, AppSchema);
            });
    }
}
