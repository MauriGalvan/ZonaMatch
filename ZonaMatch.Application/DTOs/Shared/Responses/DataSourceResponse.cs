namespace ZonaMatch.Application.DTOs.Shared.Responses;

// Source metadata for a public data point.
public sealed record DataSourceResponse(string Data, string Source, string Level, int Year);
