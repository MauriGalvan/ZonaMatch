using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Common
{
    // Contrato para entidades geo-referenciadas mediante un area (poligono/multipoligono)
    public interface IUbicacionPoligono
    {
        Geometry Geometria { get; set; }
    }
}
