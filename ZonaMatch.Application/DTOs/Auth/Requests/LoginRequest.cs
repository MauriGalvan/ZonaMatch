using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.Auth.Requests;

public sealed record LoginRequest(
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
    [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
    string Email,

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MaxLength(128, ErrorMessage = "La contrasena no puede superar los 128 caracteres.")]
    string Password)
{
    public override string ToString() => $"{nameof(LoginRequest)} {{ Email = {Email} }}";
}
