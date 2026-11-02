namespace ZonaMatch.Application.DTOs.Transit.Responses;

public sealed record TransitLineFrequencyResponse(string TimeWindow, double DeparturesPerHour, int? MinutesBetweenDepartures);
