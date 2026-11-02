using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Scoring.Requests;

public sealed class ZoneOriginRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}
