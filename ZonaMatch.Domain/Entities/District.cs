using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // District of the province of Buenos Aires (table geo.partidos)
    public class District : IPolygonLocation
    {
        public string Key { get; set; } = string.Empty;
        public int? MunicipalityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Province { get; set; } = "BUENOS AIRES";
        public string? CensusCode { get; set; }

        // Column "geom": geometry(MultiPolygon,4326)
        public Geometry Geometry { get; set; } = null!;

        public ICollection<MunicipalityAlias> Aliases { get; set; } = new List<MunicipalityAlias>();
    }
}
