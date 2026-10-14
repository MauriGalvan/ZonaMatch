using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    public record LoginDto(
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
        [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
        string Email,

        // The upper bound avoids hashing absurdly long inputs (cheap DoS on PBKDF2)
        [Required(ErrorMessage = "La contrasena es obligatoria.")]
        [MaxLength(128, ErrorMessage = "La contrasena no puede superar los 128 caracteres.")]
        string Password)
    {
        // Records print every member in ToString(): keep the password out of logs
        public override string ToString() => $"{nameof(LoginDto)} {{ Email = {Email} }}";
    }
}
