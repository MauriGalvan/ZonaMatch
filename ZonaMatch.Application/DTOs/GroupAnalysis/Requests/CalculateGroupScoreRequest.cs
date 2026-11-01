using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.GroupAnalysis.Requests;

public sealed class CalculateGroupScoreRequest
{
    [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
    public List<string> AddedZones { get; set; } = [];

    [Required]
    [MinLength(1, ErrorMessage = "Indicá el peso de cada participante.")]
    [MaxLength(5, ErrorMessage = "Un grupo puede tener hasta 5 participantes.")]
    public List<ParticipantWeightRequest> ParticipantWeights { get; set; } = [];

    // Monthly budget in pesos: rent plus expenses, or only expenses when buying
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal? MaximumRent { get; set; }

    // alquiler | compra; without it, alquiler
    [StringLength(10)]
    public string? Operation { get; set; }

    // Maximum purchase price in dollars (only when buying)
    [Range(typeof(decimal), "0", "100000000")]
    public decimal? MaximumPurchaseUsd { get; set; }

    // apartment | house | ph; without it, every type
    [StringLength(20)]
    public string? PropertyType { get; set; }

    // Surface the user looks for (m2): prices are estimated as price per m2 times this surface
    [Range(10, 2000, ErrorMessage = "La superficie debe estar entre 10 y 2.000 m².")]
    public int? SurfaceM2 { get; set; }

    // Rooms the user looks for (1 to 4, 5 = 5 or more): prices are the ones of that size in each zone
    [Range(0, 5, ErrorMessage = "Los ambientes deben estar entre 1 y 5 (5 o más).")]
    public int? Rooms { get; set; }
}
