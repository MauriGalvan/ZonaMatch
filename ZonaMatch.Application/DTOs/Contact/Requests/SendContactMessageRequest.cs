using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Contact.Requests;

public sealed record SendContactMessageRequest(
    [Required(ErrorMessage = "Ingresá tu email.")]
    [EmailAddress(ErrorMessage = "Ingresá un email válido.")]
    [StringLength(254, ErrorMessage = "El email es demasiado largo.")]
    string Email,
    [StringLength(80, ErrorMessage = "El nombre puede tener hasta 80 caracteres.")]
    string? Name,
    [StringLength(120, ErrorMessage = "La zona puede tener hasta 120 caracteres.")]
    string? Zone,
    // Honeypot field; it remains hidden and empty for real users.
    [StringLength(200)]
    string? Website);
