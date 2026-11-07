using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Analysis.Requests;

// Housing constraints and selected places.
public sealed class AnalysisPointsRequest
{
    // User-entered monthly budget cap, in ARS.
    [StringLength(30, ErrorMessage = "El alquiler máximo no puede superar 30 caracteres.")]
    public string? RentLimit { get; set; }

    public List<AnalysisPlaceRequest> Places { get; set; } = [];

    // "alquiler" or "compra"; omitted means rent.
    [StringLength(10)]
    public string? Operation { get; set; }

    [Range(typeof(decimal), "0", "100000000")]
    public decimal? PurchaseLimitUsd { get; set; }

    // "apartment", "house", or "ph"; omitted means any property type.
    [StringLength(20)]
    public string? PropertyType { get; set; }

    // Optional target area in square meters.
    [Range(0, 2000, ErrorMessage = "La superficie debe estar entre 10 y 2.000 m².")]
    public int? AreaM2 { get; set; }

    // Number of rooms (1 to 4, 5 = 5 or more): prices are those of that size; omitted or 0 uses every size.
    [Range(0, 5, ErrorMessage = "Los ambientes deben estar entre 1 y 5 (5 o más).")]
    public int? Rooms { get; set; }

    // Buying with a mortgage: compare the purchase limit with the sales eligible for it; ignored when renting.
    public bool? CreditEligible { get; set; }
}
