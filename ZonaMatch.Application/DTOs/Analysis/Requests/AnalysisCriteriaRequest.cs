using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

// The user's complete criteria, used by profile updates and analysis versions.
public sealed class AnalysisCriteriaRequest
{
    [Required(ErrorMessage = "El contexto es obligatorio.")]
    public AnalysisContextRequest Context { get; set; } = null!;

    // Keys are criterion catalog IDs; values are the selected priority values.
    [Required(ErrorMessage = "Los criterios son obligatorios.")]
    public Dictionary<string, string?> Criteria { get; set; } = null!;

    [Required(ErrorMessage = "Los puntos son obligatorios.")]
    public AnalysisPointsRequest Points { get; set; } = null!;
}
