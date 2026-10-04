using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Census tract, Censo 2022 (table geo.radios)
    public class Radio : IUbicacionPoligono
    {
        public string Id { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string? Depto { get; set; }

        // Census comuna code (text), not a FK to Comuna
        public string? Comuna { get; set; }
        public string? Fraccion { get; set; }

        // Column "radio" (a property cannot share the name of its enclosing type)
        public string? NroRadio { get; set; }

        // Column "geom": geometry(MultiPolygon,4326)
        public Geometry Geometria { get; set; } = null!;
    }
}
