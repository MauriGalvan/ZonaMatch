namespace ZonaMatch.Application.DTOs.ZoneIndicators.Responses;

// Value for the same indicator in a broader area, such as CABA.
public sealed record IndicatorReferenceResponse(string Scope, double? Value);
