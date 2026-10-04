namespace ZonaMatch.Application.DTOs
{
    // Map viewport in WGS84 degrees (what Leaflet's map.getBounds() returns)
    public record BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat)
    {
        public bool EsValido =>
            MinLat is >= -90 and <= 90 && MaxLat is >= -90 and <= 90
            && MinLon is >= -180 and <= 180 && MaxLon is >= -180 and <= 180
            && MinLon < MaxLon && MinLat < MaxLat;
    }
}
