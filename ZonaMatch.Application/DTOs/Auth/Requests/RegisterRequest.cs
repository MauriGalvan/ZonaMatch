using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Auth.Requests;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
    [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
    string Email,

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contrasena debe tener entre 8 y 128 caracteres.")]
    string Password,

    [Required(ErrorMessage = "Debes repetir la contrasena.")]
    string ConfirmPassword,

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    DateOnly? BirthDate) : IValidatableObject
{
    private const int MinimumAgeYears = 18;
    private const int MaximumAgeYears = 120;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Password != ConfirmPassword)
            yield return new ValidationResult("Las contrasenas no coinciden.", [nameof(ConfirmPassword)]);

        if (BirthDate is not { } birthDate)
            yield break; // [Required] already reports it

        var timeProvider = validationContext.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (birthDate > today || birthDate < today.AddYears(-MaximumAgeYears))
            yield return new ValidationResult("La fecha de nacimiento no es valida.", [nameof(BirthDate)]);
        else if (birthDate > today.AddYears(-MinimumAgeYears))
            yield return new ValidationResult("Debes ser mayor de edad para registrarte.", [nameof(BirthDate)]);
    }

    public override string ToString() => $"{nameof(RegisterRequest)} {{ Email = {Email} }}";
}
