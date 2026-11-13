using ZonaMatch.Application.DTOs.Geolocation;
using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Geolocation
{
    public sealed class SearchPlaces(IPlaceSearchClient placeSearchClient, IPlaceSearchClientSelector? clientSelector = null)
    {
        private const int ResultLimit = 8;

        public Task<IReadOnlyList<PlaceSearchResultResponse>> ExecuteAsync(
            string search, CancellationToken cancellationToken) =>
            ExecuteAsync(search, null, cancellationToken);

        // provider: "nominatim" or "photon" to compare engines; null uses the configured default.
        public async Task<IReadOnlyList<PlaceSearchResultResponse>> ExecuteAsync(
            string search, string? provider, CancellationToken cancellationToken)
        {
            var client = provider is null || clientSelector is null
                ? placeSearchClient
                : clientSelector.Resolve(provider);

            IReadOnlyList<PlaceSearchResult> results = [];
            var isApproximate = false;

            // Nominatim needs literal text, so it gets several rewrites; Photon only gets the cleaned-up query
            var variants = client.UsesQueryVariants
                ? PlaceQueryVariants.Build(search)
                : PlaceQueryVariants.BuildForTolerantEngines(search);

            // The first variant that finds something wins
            foreach (var (query, approximate) in variants)
            {
                results = await client.SearchAsync(query, ResultLimit, cancellationToken);
                isApproximate = approximate;
                if (results.Count > 0)
                    break;
            }

            return results
                .Select(result => new PlaceSearchResultResponse(
                    result.Id,
                    result.Name,
                    result.Kind,
                    result.Locality,
                    result.Latitude,
                    result.Longitude,
                    result.IsApproximate || isApproximate))
                .ToList();
        }
    }
}
