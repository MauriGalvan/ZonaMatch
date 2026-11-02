namespace ZonaMatch.Application.DTOs.Transit.Responses;

// Combined departures per hour for all lines in a time window.
public sealed record TransitTimeWindowResponse(
    string TimeWindow,
    int FromHour,
    int ToHour,
    int ActiveLines,
    double DeparturesPerHour);
