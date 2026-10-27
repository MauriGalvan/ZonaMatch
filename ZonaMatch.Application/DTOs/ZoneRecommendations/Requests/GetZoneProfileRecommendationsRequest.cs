namespace ZonaMatch.Application.DTOs.ZoneRecommendations.Requests;

public sealed class GetZoneProfileRecommendationsRequest
{
    public string? AgeRanges { get; init; }
    public bool HasPets { get; init; }
    public bool HasVehicle { get; init; }
    public bool HasBicycle { get; init; }

    // Starting point chosen on the map inside the zone; without it, distances are measured from the zone center
    [System.ComponentModel.DataAnnotations.Range(-90, 90)]
    public double? Latitude { get; init; }

    [System.ComponentModel.DataAnnotations.Range(-180, 180)]
    public double? Longitude { get; init; }
}
