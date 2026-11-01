using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

namespace ZonaMatch.Application.DTOs
{
    /// <summary>Request body for comparing saved analyses on the versioned API.</summary>
    public sealed class CompareAnalysesRequest
    {
        [Required]
        [MinLength(2, ErrorMessage = "Elegí al menos dos análisis para comparar.")]
        [MaxLength(5, ErrorMessage = "Se pueden comparar hasta cinco análisis a la vez.")]
        public List<Guid> AnalysisIds { get; set; } = [];
    }

    public sealed record AnalysisComparisonResponse(IReadOnlyList<ComparedAnalysisResponse> Analyses);

    public sealed record ComparedAnalysisResponse(
        Guid Id,
        string Name,
        string TravelMode,
        IReadOnlyList<ComparedZoneResponse> Zones,
        GroupRankingResponse? GroupScores = null);

    public sealed record ComparedZoneResponse(
        string Slug,
        string Name,
        int? Score,
        IReadOnlyList<ComparedScoreComponentResponse> Components,
        IReadOnlyList<ComparedScoreLimitResponse> Limits,
        IReadOnlyList<ComparedPlaceTravelTimeResponse> Places,
        ComparedRentResponse? Rent,
        ComparedSaleResponse? Sale = null);

    public sealed record ComparedScoreComponentResponse(string Criterion, string Priority, int? Score, string Details);

    public sealed record ComparedScoreLimitResponse(string Type, bool? IsMet, string Details);

    public sealed record ComparedPlaceTravelTimeResponse(
        string Name,
        int? Minutes,
        int MaximumMinutes,
        bool IsEstimated,
        double? DistanceKm = null);

    public sealed record ComparedRentResponse(
        decimal Average,
        int AvailableProperties,
        decimal? Expenses = null,
        decimal? MonthlyCost = null,
        decimal? PricePerM2 = null,
        int? SurfaceM2 = null,
        decimal? Estimated = null);

    public sealed record ComparedSaleResponse(
        decimal AverageUsd,
        int AvailableProperties,
        decimal? Expenses = null,
        decimal? PricePerM2Usd = null,
        int? SurfaceM2 = null,
        decimal? EstimatedUsd = null);

    /// <summary>Legacy request model retained for the unversioned comparison route.</summary>
    public sealed class LegacyCompareAnalysesRequest
    {
        [Required]
        [MinLength(2, ErrorMessage = "Elegí al menos dos análisis para comparar.")]
        [MaxLength(5, ErrorMessage = "Se pueden comparar hasta cinco análisis a la vez.")]
        [JsonPropertyName("analisisIds")]
        public List<Guid> AnalysisIds { get; set; } = [];
    }

    /// <summary>Legacy response model retained for existing clients of the unversioned route.</summary>
    public sealed record LegacyAnalysisComparisonResponse(
        [property: JsonPropertyName("analisis")] IReadOnlyList<LegacyComparedAnalysisResponse> Analyses);

    public sealed record LegacyComparedAnalysisResponse(
        Guid Id,
        [property: JsonPropertyName("nombre")] string Name,
        [property: JsonPropertyName("modoViaje")] string TravelMode,
        [property: JsonPropertyName("zonas")] IReadOnlyList<PuntajeZonaDto> Zones,
        [property: JsonPropertyName("puntajesEnGrupo")] GroupRankingResponse? GroupScores = null);
}
