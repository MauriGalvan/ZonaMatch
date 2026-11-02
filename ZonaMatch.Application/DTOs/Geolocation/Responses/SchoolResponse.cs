namespace ZonaMatch.Application.DTOs.Geolocation.Responses;

public sealed record SchoolResponse(
    string ClaveNatural,
    string Nombre,
    string? Nivel,
    string? Sector,
    string? Direccion,
    string? Localidad,
    double Latitud,
    double Longitud,
    double? DistanciaMetros = null);
