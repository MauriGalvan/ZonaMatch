using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Common
{
    // Contrato para entidades geo-referenciadas mediante un unico punto (latitud/longitud)
    public interface IUbicacionPunto
    {
        Point Ubicacion { get; set; }
    }
}
