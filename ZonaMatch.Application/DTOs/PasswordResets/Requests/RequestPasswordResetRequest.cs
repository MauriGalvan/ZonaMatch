using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.PasswordResets.Requests;

// POST /api/v1/password-resets: asks for an email with a link to choose a new password
public sealed class RequestPasswordResetRequest
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
    public string Email { get; set; } = string.Empty;
}
