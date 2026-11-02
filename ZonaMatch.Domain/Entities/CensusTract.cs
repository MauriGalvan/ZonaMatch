using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Census tract from the 2022 census (table geo.radios).
    public class CensusTract : IPolygonLocation
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? DepartmentCode { get; set; }

        // Census comuna code (text), not a FK to Commune
        public string? CommuneCode { get; set; }
        public string? FractionCode { get; set; }

        // Column "radio" (a property cannot share the name of its enclosing type)
        public string? RadioNumber { get; set; }

        // Column "geom": geometry(MultiPolygon,4326)
        public Geometry Geometry { get; set; } = null!;
    }
}
