using ZonaMatch.Application.DTOs.Geolocation;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPlaceSearchClient
    {
        // Nominatim only matches text almost literally, so the use case retries normalized variants of the query.
        // Engines that tolerate typos and prefixes (Photon) override this with false and get a single request.
        bool UsesQueryVariants => true;

        Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
            string search, int limit, CancellationToken cancellationToken = default);
    }
}
