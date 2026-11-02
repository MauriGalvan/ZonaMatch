namespace ZonaMatch.Application.DTOs.Geolocation.Responses;

// A point's name and address. Only coordinates are required because arbitrary map points may not have an address.
public sealed record ReverseGeocodeResponse(
    CoordinateResponse Location,
    string? Name,
    string? Street,
    string? StreetNumber,
    string? Locality,
    string? District,
    string? Commune,
    string? Province,
    string? PostalCode,
    string? FullAddress);
