using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

/// <summary>Legacy request model retained for the unversioned comparison route.</summary>
public sealed class LegacyCompareAnalysesRequest
{
    [Required]
    [MinLength(2, ErrorMessage = "Elegí al menos dos análisis para comparar.")]
    [MaxLength(5, ErrorMessage = "Se pueden comparar hasta cinco análisis a la vez.")]
    [JsonPropertyName("analisisIds")]
    public List<Guid> AnalysisIds { get; set; } = [];
}
