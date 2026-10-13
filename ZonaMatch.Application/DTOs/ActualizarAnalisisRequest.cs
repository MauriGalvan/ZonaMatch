using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // PUT /Analisis/{id} — update name and/or any of the payload blocks.
    // A block sent as null is left untouched; a block sent with a value replaces
    // its rows entirely (same semantics as the old jsonb columns).
    public class ActualizarAnalisisRequest
    {
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 50 caracteres.")]
        public string? Nombre { get; set; }

        public ContextoRequest? Contexto { get; set; }

        public Dictionary<string, string?>? Criterios { get; set; }

        public PuntosRequest? Puntos { get; set; }
    }
}
