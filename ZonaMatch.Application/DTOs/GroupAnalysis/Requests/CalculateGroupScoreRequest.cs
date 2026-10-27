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
}
