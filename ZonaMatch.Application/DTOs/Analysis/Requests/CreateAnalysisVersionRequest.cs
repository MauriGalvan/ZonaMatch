using System.ComponentModel.DataAnnotations;
using DomainAnalysis = ZonaMatch.Domain.Entities.Analysis;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

public sealed class CreateAnalysisVersionRequest
{
    [StringLength(DomainAnalysis.MaximumNameLength, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "El criterio es obligatorio.")]
    public AnalysisCriteriaRequest Criterion { get; set; } = null!;

    public List<AnalysisZoneRequest>? Zones { get; set; }
}
