namespace ZonaMatch.Application.DTOs.Geolocation.Responses
{
    public sealed record PlaceSearchResultResponse(
        string Id,
        string Name,
        string Kind,
        string? Locality,
        double Latitude,
        double Longitude,
        bool IsApproximate);
}
