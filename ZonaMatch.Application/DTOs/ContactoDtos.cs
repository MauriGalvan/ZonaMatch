using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // POST /Contacto: formulario de contacto de la landing, para avisarle las novedades a quien deja su email
    public record FormularioContactoDto(
        [Required(ErrorMessage = "Ingresa tu email.")]
        [EmailAddress(ErrorMessage = "Ingresa un email valido.")]
        [StringLength(254, ErrorMessage = "El email es demasiado largo.")]
        string Email,
        [StringLength(80, ErrorMessage = "El nombre puede tener hasta 80 caracteres.")]
        string? Nombre,
        [StringLength(120, ErrorMessage = "La zona puede tener hasta 120 caracteres.")]
        string? Zona,
        // Trampa para bots: el campo esta oculto en el formulario, una persona lo deja vacio
        [StringLength(200)]
        string? Sitio);

    // Casilla del equipo que recibe los avisos (Contacto:Destinatario)
    public record ContactoOpciones(string Destinatario);
}
