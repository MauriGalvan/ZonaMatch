using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Text;

namespace ZonaMatch.Domain.Territory
{
    // zona de análisis: un barrio de CABA o una localidad del GBA
    public class Zone
    {
        public const string CabaName = "CABA";

        public Zone(long territorialUnitId, string slug, string name, string parentName)
        {
            TerritorialUnitId = Guard.Positive(territorialUnitId, nameof(territorialUnitId));
            Slug = Guard.NotBlank(slug, nameof(slug));
            Name = Guard.NotBlank(name, nameof(name));
            ParentName = Guard.NotBlank(parentName, nameof(parentName));
        }

        public int Id { get; private set; }
        public long TerritorialUnitId { get; private set; }
        public TerritorialUnit? TerritorialUnit { get; private set; }
        public string Slug { get; private set; }
        public string Name { get; private set; }
        public string ParentName { get; private set; }

        public string DisplayName => $"{Name}, {ParentName}";

        public static bool IsAnalyzable(TerritorialUnitType type)
        {
            return type is TerritorialUnitType.Barrio or TerritorialUnitType.Localidad;
        }

        // barrio -> "Villa Luro, CABA"; localidad -> "Ramos Mejía, La Matanza"
        public static Zone Create(long territorialUnitId, TerritorialUnitType type, string unitName, string? partidoName)
        {
            if (!IsAnalyzable(type))
            {
                throw new ArgumentException($"Una unidad de tipo {type} no es una zona de análisis.", nameof(type));
            }

            var parentName = type == TerritorialUnitType.Barrio
                ? CabaName
                : Guard.NotBlank(partidoName, nameof(partidoName));

            return new Zone(territorialUnitId, TextNormalizer.Slugify(unitName, parentName), unitName, parentName);
        }

        public void UpdateFrom(Zone source)
        {
            ArgumentNullException.ThrowIfNull(source);

            Slug = source.Slug;
            Name = source.Name;
            ParentName = source.ParentName;
        }

        public void DisambiguateSlug(int suffix)
        {
            Slug = $"{Slug}-{Guard.Positive(suffix, nameof(suffix))}";
        }
    }

    public class ZoneAdjacency
    {
        public ZoneAdjacency(int zoneId, int neighborZoneId)
        {
            if (zoneId == neighborZoneId)
            {
                throw new ArgumentException("Una zona no puede ser vecina de sí misma.", nameof(neighborZoneId));
            }

            ZoneId = Guard.Positive(zoneId, nameof(zoneId));
            NeighborZoneId = Guard.Positive(neighborZoneId, nameof(neighborZoneId));
        }

        public int ZoneId { get; private set; }
        public int NeighborZoneId { get; private set; }
    }
}
