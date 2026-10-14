using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    public record RegistroUsuarioDto(
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
        [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
        string Email,

        [Required(ErrorMessage = "La contrasena es obligatoria.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "La contrasena debe tener entre 8 y 128 caracteres.")]
        string Password,

        [Required(ErrorMessage = "Debes repetir la contrasena.")]
        string ConfirmarPassword,

        // Nullable so a missing value is reported as "required" instead of binding to 0001-01-01
        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        DateOnly? FechaNacimiento) : IValidatableObject
    {
        private const int EdadMaxima = 120;

        // Runs only when the attribute validations above passed
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Password != ConfirmarPassword)
                yield return new ValidationResult("Las contrasenas no coinciden.", [nameof(ConfirmarPassword)]);

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            if (FechaNacimiento > hoy || FechaNacimiento < hoy.AddYears(-EdadMaxima))
                yield return new ValidationResult("La fecha de nacimiento no es valida.", [nameof(FechaNacimiento)]);
        }

        // Records print every member in ToString(): keep the passwords out of logs
        public override string ToString() => $"{nameof(RegistroUsuarioDto)} {{ Email = {Email} }}";
    }
}
