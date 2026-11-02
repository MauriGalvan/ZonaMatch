using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Scoring.Requests;

public sealed class PlaceScoreRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    // Muy importante | Alta | Media | Baja.
    [StringLength(20)]
    public string? Priority { get; set; }

    // Example: "45 min".
    [StringLength(10)]
    public string? MaximumTravelTime { get; set; }
}
