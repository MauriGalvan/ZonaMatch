using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

// A place selected in the analysis wizard.
public sealed class AnalysisPlaceRequest
{
    [Required(ErrorMessage = "El nombre del lugar es obligatorio.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre del lugar debe tener entre 1 y 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La dirección no puede superar 200 caracteres.")]
    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [StringLength(30)]
    public string? Type { get; set; }

    [StringLength(50)]
    public string? WhoGoes { get; set; }

    [StringLength(20)]
    public string? Frequency { get; set; }

    [StringLength(20)]
    public string? Priority { get; set; }

    [StringLength(10)]
    public string? MaximumTravelTime { get; set; }
}
