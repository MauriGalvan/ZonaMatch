namespace ZonaMatch.Application.DTOs.PointsOfInterest.Responses;

// A point of interest classified in one of the application's categories.
// The ID is stable in OSM (n=node, w=way, r=relation) or starts with a for a community contribution.
public sealed record PointOfInterestResponse(
    string Id,
    string? Nombre,
    string Categoria,
    string? Tipo,
    string? Operador,
    double Latitud,
    double Longitud,
    double DistanciaMetros,
    string Fuente);
