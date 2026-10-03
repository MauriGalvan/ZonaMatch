using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;

namespace ZonaMatch.Infrastructure.Data.Configurations
{
    public class PoiCategoryConfiguration : IEntityTypeConfiguration<PoiCategory>
    {
        public void Configure(EntityTypeBuilder<PoiCategory> builder)
        {
            builder.ToTable("poi_categories");
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
            builder.Property(category => category.Code).HasMaxLength(64).IsRequired();
            builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
            builder.Ignore(category => category.IsRoot);

            builder.HasOne<PoiCategory>()
                .WithMany()
                .HasForeignKey(category => category.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(category => category.Code).IsUnique();
            builder.HasData(CatalogSeed.Categories);
        }
    }

    public class PoiMappingRuleConfiguration : IEntityTypeConfiguration<PoiMappingRule>
    {
        public void Configure(EntityTypeBuilder<PoiMappingRule> builder)
        {
            builder.ToTable("poi_mapping_rules");
            builder.HasKey(rule => rule.Id);
            builder.Property(rule => rule.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
            builder.Property(rule => rule.Attribute).HasMaxLength(64).IsRequired();
            builder.Property(rule => rule.Value).HasMaxLength(128).IsRequired();

            builder.HasOne<DataSource>()
                .WithMany()
                .HasForeignKey(rule => rule.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<PoiCategory>()
                .WithMany()
                .HasForeignKey(rule => rule.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(rule => new { rule.DataSourceId, rule.Attribute, rule.Value }).IsUnique();
            builder.HasData(CatalogSeed.Rules);
        }
    }

    public class PointOfInterestConfiguration : IEntityTypeConfiguration<PointOfInterest>
    {
        public void Configure(EntityTypeBuilder<PointOfInterest> builder)
        {
            builder.ToTable("points_of_interest");
            builder.HasKey(poi => poi.Id);
            builder.Property(poi => poi.Id).UseIdentityByDefaultColumn();
            builder.Property(poi => poi.Name).HasMaxLength(300).IsRequired();
            builder.Property(poi => poi.NormalizedName).HasMaxLength(300).IsRequired();
            builder.Property(poi => poi.Address).HasMaxLength(300);
            builder.Property(poi => poi.Location).HasColumnType("geometry(Point,4326)").IsRequired();
            builder.Property(poi => poi.Footprint).HasColumnType("geometry(Geometry,4326)");
            builder.Property(poi => poi.ExternalId).HasMaxLength(128).IsRequired();

            // atributos propios de cada fuente sin ampliar el esquema
            builder.Property(poi => poi.Attributes)
                .HasColumnType("jsonb")
                .HasConversion(
                    attributes => JsonSerializer.Serialize(attributes, JsonSerializerOptions.Default),
                    json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions.Default)!,
                    new ValueComparer<Dictionary<string, string>>(
                        (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                        attributes => attributes.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                        attributes => new Dictionary<string, string>(attributes)));

            builder.HasOne(poi => poi.Category)
                .WithMany()
                .HasForeignKey(poi => poi.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<DataSource>()
                .WithMany()
                .HasForeignKey(poi => poi.DataSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PointOfInterest>()
                .WithMany()
                .HasForeignKey(poi => poi.CanonicalPoiId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(poi => poi.Location).HasMethod("gist");
            builder.HasIndex(poi => poi.CategoryId);
            builder.HasIndex(poi => new { poi.DataSourceId, poi.ExternalId }).IsUnique();
            builder.HasIndex(poi => poi.CanonicalPoiId);
        }
    }
}
