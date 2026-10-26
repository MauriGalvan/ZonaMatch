using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.GuestReports.Requests;

// POST /api/v1/guest-reports (multipart/form-data): a visitor without an account asks for the PDF of their analysis.
// The PDF goes in the "report" file field.
public sealed class SendGuestReportRequest
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    // Hidden field that only bots fill in
    public string? Website { get; set; }
}
