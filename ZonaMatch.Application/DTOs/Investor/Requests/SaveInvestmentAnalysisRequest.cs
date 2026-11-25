using System.ComponentModel.DataAnnotations;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using DomainAnalysis = ZonaMatch.Domain.Entities.Analysis;

namespace ZonaMatch.Application.DTOs.Investor.Requests;

// An investment analysis to save: its zones and the investor settings it was calculated with. For a version the name
// is optional, as for the household analyses.
public sealed class SaveInvestmentAnalysisRequest
{
    [StringLength(DomainAnalysis.MaximumNameLength, ErrorMessage = "El nombre debe tener hasta {1} caracteres.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "La configuración del análisis es obligatoria.")]
    public InvestorProfileRequest Settings { get; set; } = new();

    [Required(ErrorMessage = "Las zonas son obligatorias.")]
    [MinLength(1, ErrorMessage = "Elegí al menos una zona.")]
    public List<AnalysisZoneRequest> Zones { get; set; } = [];
}
