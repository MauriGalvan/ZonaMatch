using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Infrastructure.Data.Configurations
{
    public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
    {
        public void Configure(EntityTypeBuilder<DataSource> builder)
        {
            builder.ToTable("data_sources");
            builder.HasKey(source => source.Id);
            builder.Property(source => source.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
            builder.Property(source => source.Code).HasMaxLength(64).IsRequired();
            builder.Property(source => source.Name).HasMaxLength(200).IsRequired();
            builder.Property(source => source.Publisher).HasMaxLength(200);
            builder.Property(source => source.Url).HasMaxLength(500);
            builder.Property(source => source.License).HasMaxLength(100);
            builder.HasIndex(source => source.Code).IsUnique();
            builder.HasData(CatalogSeed.DataSources);
        }
    }

    public class TerritorialUnitConfiguration : IEntityTypeConfiguration<TerritorialUnit>
    {
        public void Configure(EntityTypeBuilder<TerritorialUnit> builder)
        {
            builder.ToTable("territorial_units");
            builder.HasKey(unit => unit.Id);
            builder.Property(unit => unit.Id).UseIdentityByDefaultColumn();
            builder.Property(unit => unit.Code).HasMaxLength(32);
            builder.Property(unit => unit.Name).HasMaxLength(200).IsRequired();
            builder.Property(unit => unit.NormalizedName).HasMaxLength(200).IsRequired();
            builder.Property(unit => unit.Geometry).HasColumnType("geometry(MultiPolygon,4326)").IsRequired();
            builder.Property(unit => unit.ExternalId).HasMaxLength(128).IsRequired();

            builder.HasOne(unit => unit.Parent)
                .WithMany()
                .HasForeignKey(unit => unit.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<DataSource>()
                .WithMany()
                .HasForeignKey(unit => unit.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(unit => unit.Geometry).HasMethod("gist");
            builder.HasIndex(unit => new { unit.Type, unit.NormalizedName });
            builder.HasIndex(unit => new { unit.Type, unit.Code }).IsUnique().HasFilter("code IS NOT NULL");
            builder.HasIndex(unit => new { unit.DataSourceId, unit.ExternalId }).IsUnique();
            builder.HasIndex(unit => unit.ParentId);
        }
    }

    public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
    {
        public void Configure(EntityTypeBuilder<Zone> builder)
        {
            builder.ToTable("zones");
            builder.HasKey(zone => zone.Id);
            builder.Property(zone => zone.Id).UseIdentityByDefaultColumn();
            builder.Property(zone => zone.Slug).HasMaxLength(160).IsRequired();
            builder.Property(zone => zone.Name).HasMaxLength(200).IsRequired();
            builder.Property(zone => zone.ParentName).HasMaxLength(200).IsRequired();
            builder.Ignore(zone => zone.DisplayName);

            builder.HasOne(zone => zone.TerritorialUnit)
                .WithOne()
                .HasForeignKey<Zone>(zone => zone.TerritorialUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(zone => zone.Slug).IsUnique();
        }
    }

    public class ZoneAdjacencyConfiguration : IEntityTypeConfiguration<ZoneAdjacency>
    {
        public void Configure(EntityTypeBuilder<ZoneAdjacency> builder)
        {
            builder.ToTable("zone_adjacencies");
            builder.HasKey(adjacency => new { adjacency.ZoneId, adjacency.NeighborZoneId });

            builder.HasOne<Zone>()
                .WithMany()
                .HasForeignKey(adjacency => adjacency.ZoneId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Zone>()
                .WithMany()
                .HasForeignKey(adjacency => adjacency.NeighborZoneId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class TerritorialIndicatorConfiguration : IEntityTypeConfiguration<TerritorialIndicator>
    {
        public void Configure(EntityTypeBuilder<TerritorialIndicator> builder)
        {
            builder.ToTable("territorial_indicators");
            builder.HasKey(indicator => indicator.Id);
            builder.Property(indicator => indicator.Id).UseIdentityByDefaultColumn();
            builder.Property(indicator => indicator.IndicatorCode).HasMaxLength(64).IsRequired();
            builder.Property(indicator => indicator.Value).HasPrecision(18, 4);
            builder.Property(indicator => indicator.Unit).HasMaxLength(32).IsRequired();

            builder.HasOne<TerritorialUnit>()
                .WithMany()
                .HasForeignKey(indicator => indicator.TerritorialUnitId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<DataSource>()
                .WithMany()
                .HasForeignKey(indicator => indicator.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(indicator => new
            {
                indicator.TerritorialUnitId,
                indicator.IndicatorCode,
                indicator.DataSourceId,
                indicator.ReferenceDate,
            }).IsUnique();
        }
    }
}
