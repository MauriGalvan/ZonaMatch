using ZonaMatch.Application.DTOs.Geolocation;
using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Geolocation
{
    public sealed class SearchPlaces(IPlaceSearchClient placeSearchClient)
    {
        private const int ResultLimit = 8;

        public async Task<IReadOnlyList<PlaceSearchResultResponse>> ExecuteAsync(
            string search, CancellationToken cancellationToken)
        {
            IReadOnlyList<PlaceSearchResult> results = [];
            var isApproximate = false;

            // The first variant that finds something wins: as typed, normalized, then without the number
            foreach (var (query, approximate) in PlaceQueryVariants.Build(search))
            {
                results = await placeSearchClient.SearchAsync(query, ResultLimit, cancellationToken);
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
