using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ZonaMatch.Application.DTOs
{
    // PUT /Analisis/{id} — update name and/or payload of an existing saved analysis.
    public class ActualizarAnalisisRequest
    {
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string? Nombre { get; set; }

        public JsonElement? Contexto { get; set; }

        public JsonElement? Criterios { get; set; }

        public JsonElement? Puntos { get; set; }
    }
}
