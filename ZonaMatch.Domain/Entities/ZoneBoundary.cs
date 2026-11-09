namespace ZonaMatch.Domain.Entities
{
    // Where a zone is drawn: a point inside it (WGS84) and its simplified boundary as a GeoJSON geometry.
    public sealed record ZoneBoundary(double CenterLatitude, double CenterLongitude, string GeometryGeoJson);
}
