namespace ZonaMatch.Domain.Entities;

// Path of a transit line inside a zone: GeoJSON LineString or MultiLineString in WGS84 (longitude, latitude).
public sealed class TransitRouteGeometry
{
    public string Mode { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public string GeometryGeoJson { get; set; } = string.Empty;
}
