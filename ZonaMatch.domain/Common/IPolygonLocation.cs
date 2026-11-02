using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Common;

/// <summary>Contract for entities located within a polygon or multipolygon.</summary>
public interface IPolygonLocation
{
    Geometry Geometry { get; set; }
}
