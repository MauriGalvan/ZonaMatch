namespace ZonaMatch.Application.Interfaces
{
    // Picks the reverse geocoder ("osm", "nominatim" or "photon") for one request.
    public interface IGeocodingClientSelector
    {
        IGeocodingClient Resolve(string? provider);
    }
}
