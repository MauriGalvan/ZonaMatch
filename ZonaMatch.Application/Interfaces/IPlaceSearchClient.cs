using ZonaMatch.Application.DTOs.Geolocation;

namespace ZonaMatch.Application.Interfaces
{
    public interface IPlaceSearchClient
    {
        Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(
            string search, int limit, CancellationToken cancellationToken = default);
    }
}
