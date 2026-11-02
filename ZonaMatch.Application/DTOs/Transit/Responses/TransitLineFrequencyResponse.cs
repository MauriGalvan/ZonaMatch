namespace ZonaMatch.Application.DTOs.Transit.Responses;

public sealed record TransitLineFrequencyResponse(string Franja, double SalidasPorHora, int? MinutosEntreSalidas);
