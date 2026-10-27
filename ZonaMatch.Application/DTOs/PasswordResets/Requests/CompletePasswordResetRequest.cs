using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.PasswordResets.Requests;

// POST /api/v1/password-resets/confirmation: the token of the emailed link and the new password
public sealed class CompletePasswordResetRequest : IValidatableObject
{
    [Required(ErrorMessage = "Falta el enlace para cambiar la contraseña.")]
    [MaxLength(200, ErrorMessage = "El enlace no es válido.")]
    public string Token { get; set; } = string.Empty;

    // Same rules as the registration
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 128 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tenés que repetir la contraseña.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Password != ConfirmPassword)
            yield return new ValidationResult("Las contraseñas no coinciden.", [nameof(ConfirmPassword)]);
    }
}
