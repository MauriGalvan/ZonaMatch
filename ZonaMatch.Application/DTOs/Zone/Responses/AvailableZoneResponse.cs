namespace ZonaMatch.Application.DTOs.Zone.Responses;

public sealed record AvailableZoneResponse(
    string Slug,
    string Name,
    string Type,
    bool HasData);
