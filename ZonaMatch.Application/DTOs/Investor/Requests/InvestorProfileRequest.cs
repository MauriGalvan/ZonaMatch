using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Investor.Requests;

// Body of PUT /api/v1/investor-profile.
public sealed class InvestorProfileRequest
{
    // Investment criteria and their priority (baja, media, alta), from GET /api/v1/criteria?perfil=inversor
    public Dictionary<string, string?>? Criteria { get; set; }

    // Money available for the purchase, in dollars
    [Required(ErrorMessage = "Indicá el capital disponible en dólares.")]
    [Range(typeof(decimal), "1000", "100000000", ErrorMessage = "El capital debe estar entre US$ 1.000 y US$ 100.000.000.")]
    public decimal? CapitalUsd { get; set; }

    // Buying with a mortgage: the capital is compared with the sales eligible for it ("apto crédito")
    public bool CreditEligible { get; set; }

    // apartment | house | ph; without it, every type
    [StringLength(20)]
    public string? PropertyType { get; set; }

    // 1 to 4, 5 = 5 or more; without it, every size
    [Range(1, 5, ErrorMessage = "Los ambientes deben estar entre 1 y 5 (5 o más).")]
    public int? Rooms { get; set; }
}
