using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Common;

/// <summary>Contract for entities located at a single geographic point.</summary>
public interface IPointLocation
{
    Point Location { get; set; }
}
