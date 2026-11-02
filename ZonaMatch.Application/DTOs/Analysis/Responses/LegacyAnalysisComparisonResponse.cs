using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

/// <summary>Legacy response model retained for existing clients of the unversioned route.</summary>
public sealed record LegacyAnalysisComparisonResponse(
    [property: JsonPropertyName("analisis")] IReadOnlyList<LegacyComparedAnalysisResponse> Analyses);
