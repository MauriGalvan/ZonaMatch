using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Partido of the province of Buenos Aires (table geo.partidos)
    public class Partido : IUbicacionPoligono
    {
        public string Key { get; set; } = string.Empty;
        public int? MunicipioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Provincia { get; set; } = "BUENOS AIRES";
        public string? CensoCodigo { get; set; }

        // Column "geom": geometry(MultiPolygon,4326)
        public Geometry Geometria { get; set; } = null!;

        public ICollection<MunicipioAlias> Aliases { get; set; } = new List<MunicipioAlias>();
    }
}
