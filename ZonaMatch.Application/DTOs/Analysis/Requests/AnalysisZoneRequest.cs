using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

public sealed class AnalysisZoneRequest
{
    [Required(ErrorMessage = "El slug de la zona es obligatorio.")]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de la zona es obligatorio.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsInitial { get; set; }

    public bool IsAdded { get; set; }

    // Starting point marked inside the zone; null = the center of the zone
    public AnalysisZoneOriginRequest? Origin { get; set; }
}

public sealed class AnalysisZoneOriginRequest
{
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public double Longitude { get; set; }
}
