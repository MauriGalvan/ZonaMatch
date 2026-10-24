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
            var results = await placeSearchClient.SearchAsync(search.Trim(), ResultLimit, cancellationToken);

            return results
                .Select(result => new PlaceSearchResultResponse(
                    result.Id,
                    result.Name,
                    result.Kind,
                    result.Locality,
                    result.Latitude,
                    result.Longitude,
                    result.IsApproximate))
                .ToList();
        }
    }
}
