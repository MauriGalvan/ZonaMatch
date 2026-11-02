using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Scoring.Requests;

/// <summary>Input used to calculate an automatic zone ranking.</summary>
public sealed class CalculateScoresRequest
{
    [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
    public List<string> AddedZones { get; set; } = [];

    [StringLength(100)]
    public string? InitialZone { get; set; }

    // Key: criterion ID (seguridad, movilidad, espacios-verdes, educacion, salud); value: baja|media|alta.
    public Dictionary<string, string?> Criteria { get; set; } = [];

    // Visitors: up to 3 (ScoresController); signed-in users: up to 15.
    [MaxLength(15, ErrorMessage = "Se pueden medir hasta 15 lugares.")]
    public List<PlaceScoreRequest> Places { get; set; } = [];

    // Maximum monthly expense in Argentine pesos, as entered by the user.
    [StringLength(30)]
    public string? MaximumRent { get; set; }

    // alquiler | compra; defaults to alquiler when omitted.
    [StringLength(10)]
    public string? Operation { get; set; }

    [Range(typeof(decimal), "0", "100000000")]
    public decimal? MaximumPurchasePriceUsd { get; set; }

    // apartment | house | ph; all housing types when omitted.
    [StringLength(20)]
    public string? HousingType { get; set; }

    [Range(0, 2000, ErrorMessage = "La superficie debe estar entre 10 y 2.000 m².")]
    public int? AreaM2 { get; set; }

    // Number of rooms (1 to 4, 5 = 5 or more): prices are those of that size; omitted or 0 uses every size.
    [Range(0, 5, ErrorMessage = "Los ambientes deben estar entre 1 y 5 (5 o más).")]
    public int? Rooms { get; set; }

    // Mobility options (walking, public transit, car, cycling) determine how trips are measured.
    public List<string> Mobility { get; set; } = [];

    // Starting points chosen inside a zone instead of its center.
    [MaxLength(10, ErrorMessage = "Se pueden elegir hasta 10 puntos de partida.")]
    public List<ZoneOriginRequest> ZoneOrigins { get; set; } = [];
}
