namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Departures per hour and minutes between departures for each time window.
public sealed record TransitLineResponse(
    string Linea,
    int Paradas,
    IReadOnlyList<TransitLineFrequencyResponse> Franjas);
