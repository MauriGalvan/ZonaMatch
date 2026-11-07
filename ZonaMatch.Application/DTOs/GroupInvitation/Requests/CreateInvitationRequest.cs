using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs.GroupInvitation.Requests;

public class CreateInvitationRequest
{
    public Guid GroupId { get; set; }

    [Required(ErrorMessage = "Ingresá el email de la persona que querés invitar.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [MaxLength(254, ErrorMessage = "El email no puede superar los 254 caracteres.")]
    public string Email { get; set; } = string.Empty;
}
