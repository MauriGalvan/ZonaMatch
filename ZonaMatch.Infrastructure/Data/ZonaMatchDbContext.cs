using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Infrastructure.Data
{
    public class ZonaMatchDbContext : DbContext
    {
        // el modelo canónico vive aparte de las tablas de origen (zm.*, public.planet_osm_*)
        public const string Schema = "zonamatch";

        public ZonaMatchDbContext(DbContextOptions<ZonaMatchDbContext> options)
            : base(options)
        {
        }

        public DbSet<DataSource> DataSources => Set<DataSource>();
        public DbSet<TerritorialUnit> TerritorialUnits => Set<TerritorialUnit>();
        public DbSet<Zone> Zones => Set<Zone>();
        public DbSet<ZoneAdjacency> ZoneAdjacencies => Set<ZoneAdjacency>();
        public DbSet<TerritorialIndicator> TerritorialIndicators => Set<TerritorialIndicator>();
        public DbSet<PoiCategory> PoiCategories => Set<PoiCategory>();
        public DbSet<PoiMappingRule> PoiMappingRules => Set<PoiMappingRule>();
        public DbSet<PointOfInterest> PointsOfInterest => Set<PointOfInterest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");
            // osm2pgsql --hstore guarda todas las etiquetas de OSM en una columna hstore
            modelBuilder.HasPostgresExtension("hstore");
            modelBuilder.HasDefaultSchema(Schema);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZonaMatchDbContext).Assembly);

            ApplySnakeCaseNames(modelBuilder);
        }

        private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(StoreNaming.ToSnakeCase(property.Name));
                }

                foreach (var key in entity.GetKeys())
                {
                    key.SetName(StoreNaming.ToSnakeCase(key.GetName()!));
                }

                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    foreignKey.SetConstraintName(StoreNaming.ToSnakeCase(foreignKey.GetConstraintName()!));
                }

                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(StoreNaming.ToSnakeCase(index.GetDatabaseName()!));
                }
            }
        }
    }
}
