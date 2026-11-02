namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Combined departures per hour for all lines in a time window.
public sealed record TransitTimeWindowResponse(
    string Franja,
    int Desde,
    int Hasta,
    int LineasConServicio,
    double SalidasPorHora);
