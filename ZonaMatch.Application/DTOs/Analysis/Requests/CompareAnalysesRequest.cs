using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

/// <summary>Request body for comparing saved analyses.</summary>
public sealed class CompareAnalysesRequest
{
    [Required]
    [MinLength(2, ErrorMessage = "Elegí al menos dos análisis para comparar.")]
    [MaxLength(5, ErrorMessage = "Se pueden comparar hasta cinco análisis a la vez.")]
    public List<Guid> AnalysisIds { get; set; } = [];
}
