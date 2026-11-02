namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Mode values are colectivo, tren, or subte.
public sealed record TransitModeResponse(
    string Modo,
    int Paradas,
    int CantidadLineas,
    IReadOnlyList<TransitTimeWindowResponse> Franjas,
    // Lines with the most service during the morning peak come first.
    IReadOnlyList<TransitLineResponse> Lineas);
