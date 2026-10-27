namespace ZonaMatch.Application.DTOs.ZoneRecommendations.Requests;

public sealed class GetZoneProfileRecommendationsRequest
{
    public string? AgeRanges { get; init; }
    public bool HasPets { get; init; }
    public bool HasVehicle { get; init; }
    public bool HasBicycle { get; init; }
}
