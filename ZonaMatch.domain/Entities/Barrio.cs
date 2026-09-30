using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Barrio/localidad oficial (limite administrativo)
    public class Barrio : IUbicacionPoligono
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Comuna { get; set; }
        public int? Poblacion { get; set; }

        public Geometry Geometria { get; set; } = null!;
    }
}
