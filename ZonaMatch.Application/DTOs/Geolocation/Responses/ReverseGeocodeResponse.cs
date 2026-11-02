namespace ZonaMatch.Application.DTOs.Geolocation.Responses;

// A point's name and address. Only coordinates are required because arbitrary map points may not have an address.
public sealed record ReverseGeocodeResponse(
    CoordinateResponse Ubicacion,
    string? Nombre,
    string? Calle,
    string? Altura,
    string? Localidad,
    string? District,
    string? Commune,
    string? Provincia,
    string? CodigoPostal,
    string? DireccionCompleta);
