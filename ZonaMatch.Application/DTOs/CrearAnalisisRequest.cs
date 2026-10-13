using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // POST /Analisis — create a new saved analysis for the current user.
    public class CrearAnalisisRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El contexto es obligatorio.")]
        public ContextoRequest Contexto { get; set; } = null!;

        // Clave: id del catalogo de criterios; valor: baja|media|alta
        [Required(ErrorMessage = "Los criterios son obligatorios.")]
        public Dictionary<string, string?> Criterios { get; set; } = null!;

        [Required(ErrorMessage = "Los puntos son obligatorios.")]
        public PuntosRequest Puntos { get; set; } = null!;
    }
}
