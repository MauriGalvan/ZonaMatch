using System.ComponentModel.DataAnnotations;
using DomainAnalysis = ZonaMatch.Domain.Entities.Analysis;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

public sealed class RenameAnalysisRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(DomainAnalysis.MaximumNameLength, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string Name { get; set; } = string.Empty;
}
