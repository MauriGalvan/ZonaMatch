namespace ZonaMatch.Application.Models.Geolocation;

// Provider result before it is enriched with district and commune data from the local GIS database.
public sealed record GeocodedAddress(
    string? Name,
    string? Street,
    string? StreetNumber,
    string? Locality,
    string? Province,
    string? PostalCode,
    string? FullAddress);
