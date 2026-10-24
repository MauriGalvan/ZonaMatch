namespace ZonaMatch.Application.DTOs.Geolocation
{
    // Internal result returned by the local OSM search adapter.
    public sealed class PlaceSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Locality { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsApproximate { get; set; }
    }
}
