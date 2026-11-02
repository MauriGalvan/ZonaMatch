using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Commune of CABA (table geo.comunas)
    public class Commune : IPolygonLocation
    {
        public string Key { get; set; } = string.Empty;
        public string? CabaId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Province { get; set; } = "CIUDAD AUTONOMA DE BUENOS AIRES";
        public string? CensusCode { get; set; }

        // Column "geom": geometry(MultiPolygon,4326)
        public Geometry Geometry { get; set; } = null!;
    }
}
