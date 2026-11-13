namespace ZonaMatch.Application.Interfaces
{
    // Picks the place search engine ("nominatim" or "photon") for one request.
    public interface IPlaceSearchClientSelector
    {
        IPlaceSearchClient Resolve(string? provider);
    }
}
