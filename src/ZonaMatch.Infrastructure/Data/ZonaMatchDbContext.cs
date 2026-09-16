using Microsoft.EntityFrameworkCore;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Infrastructure.Data;

public class ZonaMatchDbContext : DbContext
{
    public ZonaMatchDbContext(DbContextOptions<ZonaMatchDbContext> options) : base(options)
    {
    }

    public DbSet<LocationEntity> Locations => Set<LocationEntity>();
    public DbSet<PoiEntity> Pois => Set<PoiEntity>();
    public DbSet<WeatherCacheEntity> WeatherCache => Set<WeatherCacheEntity>();
    public DbSet<UserProfileEntity> UserProfiles => Set<UserProfileEntity>();
    public DbSet<UserDestinationEntity> UserDestinations => Set<UserDestinationEntity>();
    public DbSet<ZoneEvaluationEntity> ZoneEvaluations => Set<ZoneEvaluationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Locations
        modelBuilder.Entity<LocationEntity>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GeorefId).HasColumnName("georef_id").HasMaxLength(50);
            entity.Property(e => e.NormalizedName).HasColumnName("normalized_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(100);
            entity.Property(e => e.Province).HasColumnName("province").HasMaxLength(100);
            entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
            entity.Property(e => e.Geom).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.Geom).HasMethod("GIST");
            entity.HasIndex(e => e.GeorefId);
        });

        // POIs
        modelBuilder.Entity<PoiEntity>(entity =>
        {
            entity.ToTable("pois");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OsmId).HasColumnName("osm_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Subcategory).HasColumnName("subcategory").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
            entity.Property(e => e.Geom).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired();
            entity.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.Geom).HasMethod("GIST");
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Subcategory);
            entity.HasIndex(e => e.OsmId).IsUnique();
        });

        // Weather Cache
        modelBuilder.Entity<WeatherCacheEntity>(entity =>
        {
            entity.ToTable("weather_cache");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GridLat).HasColumnName("grid_lat").HasPrecision(6, 2);
            entity.Property(e => e.GridLon).HasColumnName("grid_lon").HasPrecision(6, 2);
            entity.Property(e => e.Geom).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired();
            entity.Property(e => e.AvgTemperature).HasColumnName("avg_temperature").HasPrecision(4, 1);
            entity.Property(e => e.MaxTemperature).HasColumnName("max_temperature").HasPrecision(4, 1);
            entity.Property(e => e.MinTemperature).HasColumnName("min_temperature").HasPrecision(4, 1);
            entity.Property(e => e.AnnualPrecipitationMm).HasColumnName("annual_precipitation_mm").HasPrecision(6, 1);
            entity.Property(e => e.RainyDaysCount).HasColumnName("rainy_days_count");
            entity.Property(e => e.RawDataJson).HasColumnName("raw_data").HasColumnType("jsonb");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");

            entity.HasIndex(e => e.Geom).HasMethod("GIST");
            entity.HasIndex(e => new { e.GridLat, e.GridLon }).IsUnique();
        });

        // User Profiles
        modelBuilder.Entity<UserProfileEntity>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(e => e.HouseholdType).HasColumnName("household_type").HasMaxLength(50);
            entity.Property(e => e.MaxTravelTimeMinutes).HasColumnName("max_travel_time_minutes");
            entity.Property(e => e.WeightsJson).HasColumnName("weights").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasMany(e => e.Destinations)
                  .WithOne(d => d.Profile)
                  .HasForeignKey(d => d.ProfileId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Evaluations)
                  .WithOne(ev => ev.Profile)
                  .HasForeignKey(ev => ev.ProfileId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // User Destinations
        modelBuilder.Entity<UserDestinationEntity>(entity =>
        {
            entity.ToTable("user_destinations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(255);
            entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
            entity.Property(e => e.Geom).HasColumnName("geom").HasColumnType("geometry(Point, 4326)").IsRequired();
            entity.Property(e => e.TransportMode).HasColumnName("transport_mode").HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.Geom).HasMethod("GIST");
            entity.HasIndex(e => e.ProfileId);
        });

        // Zone Evaluations
        modelBuilder.Entity<ZoneEvaluationEntity>(entity =>
        {
            entity.ToTable("zone_evaluations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
            entity.Property(e => e.EvaluatedPoint).HasColumnName("evaluated_point").HasColumnType("geometry(Point, 4326)").IsRequired();
            entity.Property(e => e.AddressQuery).HasColumnName("address_query").HasMaxLength(255);
            entity.Property(e => e.TotalScore).HasColumnName("total_score").HasPrecision(5, 2);
            entity.Property(e => e.DataConfidence).HasColumnName("data_confidence").HasPrecision(5, 2);
            entity.Property(e => e.SubscoresJson).HasColumnName("subscores").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.MobilityDetailsJson).HasColumnName("mobility_details").HasColumnType("jsonb");
            entity.Property(e => e.ProsJson).HasColumnName("pros").HasColumnType("jsonb");
            entity.Property(e => e.ConsJson).HasColumnName("cons").HasColumnType("jsonb");
            entity.Property(e => e.Explanation).HasColumnName("explanation");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.EvaluatedPoint).HasMethod("GIST");
            entity.HasIndex(e => e.ProfileId);
        });
    }
}
