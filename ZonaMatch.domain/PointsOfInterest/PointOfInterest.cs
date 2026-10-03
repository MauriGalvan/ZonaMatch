using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.Text;

namespace ZonaMatch.Domain.PointsOfInterest
{
    public enum Ownership : short
    {
        Unknown = 0,
        Public = 1,
        Private = 2,
    }

    // punto de interés canónico: mismo formato venga del padrón oficial, de OSM o de un vecino
    public class PointOfInterest
    {
        public PointOfInterest(
            short categoryId,
            string name,
            string normalizedName,
            string? address,
            Point location,
            Geometry? footprint,
            Ownership ownership,
            Dictionary<string, string> attributes,
            short dataSourceId,
            string externalId,
            DateOnly? sourceDate,
            DateTimeOffset importedAt)
        {
            CategoryId = Guard.Positive(categoryId, nameof(categoryId));
            Name = Guard.NotBlank(name, nameof(name));
            NormalizedName = Guard.NotBlank(normalizedName, nameof(normalizedName));
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            Location = Guard.NotNull(location, nameof(location));
            Footprint = footprint;
            Ownership = ownership;
            Attributes = Guard.NotNull(attributes, nameof(attributes));
            DataSourceId = Guard.Positive(dataSourceId, nameof(dataSourceId));
            ExternalId = Guard.NotBlank(externalId, nameof(externalId));
            SourceDate = sourceDate;
            ImportedAt = importedAt;
        }

        public long Id { get; private set; }
        public short CategoryId { get; private set; }
        public PoiCategory? Category { get; private set; }
        public string Name { get; private set; }
        public string NormalizedName { get; private set; }
        public string? Address { get; private set; }
        public Point Location { get; private set; }
        public Geometry? Footprint { get; private set; }
        public Ownership Ownership { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; }
        public short DataSourceId { get; private set; }
        public string ExternalId { get; private set; }
        public DateOnly? SourceDate { get; private set; }
        public DateTimeOffset ImportedAt { get; private set; }
        public long? CanonicalPoiId { get; private set; }

        public static PointOfInterest Create(
            short categoryId,
            string? name,
            string fallbackName,
            string? address,
            Geometry sourceGeometry,
            Ownership ownership,
            Dictionary<string, string> attributes,
            short dataSourceId,
            string externalId,
            DateOnly? sourceDate,
            DateTimeOffset importedAt)
        {
            ArgumentNullException.ThrowIfNull(sourceGeometry);

            var location = GeometryNormalizer.ToRepresentativePoint(sourceGeometry);
            // solo se conserva la huella cuando la fuente trae una superficie
            Geometry? footprint = sourceGeometry is Point ? null : GeometryNormalizer.ToWgs84(sourceGeometry);
            var displayName = string.IsNullOrWhiteSpace(name) ? Guard.NotBlank(fallbackName, nameof(fallbackName)) : name.Trim();

            return new PointOfInterest(
                categoryId,
                displayName,
                TextNormalizer.Normalize(displayName),
                address,
                location,
                footprint,
                ownership,
                attributes,
                dataSourceId,
                externalId,
                sourceDate,
                importedAt);
        }

        public void MarkAsDuplicateOf(long canonicalPoiId)
        {
            if (canonicalPoiId == Id)
            {
                throw new ArgumentException("Un punto no puede ser duplicado de sí mismo.", nameof(canonicalPoiId));
            }

            CanonicalPoiId = Guard.Positive(canonicalPoiId, nameof(canonicalPoiId));
        }

        public void UpdateFrom(PointOfInterest source)
        {
            ArgumentNullException.ThrowIfNull(source);

            CategoryId = source.CategoryId;
            Name = source.Name;
            NormalizedName = source.NormalizedName;
            Address = source.Address;
            Location = source.Location;
            Footprint = source.Footprint;
            Ownership = source.Ownership;
            Attributes = source.Attributes;
            SourceDate = source.SourceDate;
            ImportedAt = source.ImportedAt;
        }
    }
}
