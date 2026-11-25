using System.ComponentModel.DataAnnotations;
using DomainAnalysis = ZonaMatch.Domain.Entities.Analysis;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

public sealed class CreateAnalysisRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(DomainAnalysis.MaximumNameLength, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Las zonas son obligatorias.")]
    [MinLength(1, ErrorMessage = "Elegí al menos una zona.")]
    public List<AnalysisZoneRequest> Zones { get; set; } = [];
}
