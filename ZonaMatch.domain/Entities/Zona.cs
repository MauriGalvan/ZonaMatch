using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Zona de recomendacion propia de ZonaMatch (puede agrupar uno o varios barrios)
    public class Zona : IUbicacionPoligono
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        public Geometry Geometria { get; set; } = null!;
    }
}
