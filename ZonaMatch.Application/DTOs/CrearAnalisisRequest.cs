using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ZonaMatch.Application.DTOs
{
    // POST /Analisis — create a new saved analysis for the current user.
    public class CrearAnalisisRequest
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        // { personas, edades[], movilidad[], mascotas[] }
        [Required(ErrorMessage = "El contexto es obligatorio.")]
        public JsonElement Contexto { get; set; }

        // { "seguridad": "alta", ... }
        [Required(ErrorMessage = "Los criterios son obligatorios.")]
        public JsonElement Criterios { get; set; }

        // { lugares[], alquilerMaximo }
        [Required(ErrorMessage = "Los puntos son obligatorios.")]
        public JsonElement Puntos { get; set; }
    }
}
