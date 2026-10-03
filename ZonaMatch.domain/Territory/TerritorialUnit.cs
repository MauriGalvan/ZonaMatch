using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.Text;

namespace ZonaMatch.Domain.Territory
{
    public enum TerritorialUnitType : short
    {
        Comuna = 1,
        Partido = 2,
        Barrio = 3,
        Localidad = 4,
        CensusTract = 5,
    }

    // unidad territorial canónica: comuna, partido, barrio, localidad o radio censal
    public class TerritorialUnit
    {
        // usado por EF Core y por Create; recibe los valores ya calculados
        public TerritorialUnit(
            TerritorialUnitType type,
            string? code,
            string name,
            string normalizedName,
            MultiPolygon geometry,
            double areaM2,
            short dataSourceId,
            string externalId,
            DateOnly? sourceDate,
            DateTimeOffset importedAt)
        {
            Type = type;
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
            Name = Guard.NotBlank(name, nameof(name));
            NormalizedName = Guard.NotBlank(normalizedName, nameof(normalizedName));
            Geometry = Guard.NotNull(geometry, nameof(geometry));
            AreaM2 = Guard.NotNegative(areaM2, nameof(areaM2));
            DataSourceId = Guard.Positive(dataSourceId, nameof(dataSourceId));
            ExternalId = Guard.NotBlank(externalId, nameof(externalId));
            SourceDate = sourceDate;
            ImportedAt = importedAt;
        }

        public long Id { get; private set; }
        public TerritorialUnitType Type { get; private set; }
        public string? Code { get; private set; }
        public string Name { get; private set; }
        public string NormalizedName { get; private set; }
        public long? ParentId { get; private set; }
        public TerritorialUnit? Parent { get; private set; }
        public MultiPolygon Geometry { get; private set; }
        public double AreaM2 { get; private set; }
        public short DataSourceId { get; private set; }
        public string ExternalId { get; private set; }
        public DateOnly? SourceDate { get; private set; }
        public DateTimeOffset ImportedAt { get; private set; }

        public static TerritorialUnit Create(
            TerritorialUnitType type,
            string? code,
            string name,
            Geometry sourceGeometry,
            short dataSourceId,
            string externalId,
            DateOnly? sourceDate,
            DateTimeOffset importedAt)
        {
            var geometry = GeometryNormalizer.ToMultiPolygon(sourceGeometry);
            var displayName = TextNormalizer.ToDisplayName(Guard.NotBlank(name, nameof(name)));

            return new TerritorialUnit(
                type,
                code,
                displayName,
                TextNormalizer.Normalize(displayName),
                geometry,
                GeoMath.AreaSquareMeters(geometry),
                dataSourceId,
                externalId,
                sourceDate,
                importedAt);
        }

        public void AssignParent(long parentId)
        {
            ParentId = Guard.Positive(parentId, nameof(parentId));
        }

        // reimportación: la identidad (fuente + id externo) se conserva, el resto se actualiza
        public void UpdateFrom(TerritorialUnit source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Type = source.Type;
            Code = source.Code;
            Name = source.Name;
            NormalizedName = source.NormalizedName;
            Geometry = source.Geometry;
            AreaM2 = source.AreaM2;
            SourceDate = source.SourceDate;
            ImportedAt = source.ImportedAt;
            ParentId = source.ParentId;
        }
    }
}
