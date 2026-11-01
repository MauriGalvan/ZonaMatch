using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // A user's full criteria: the profile (PUT /api/v1/profile) or an analysis version adjustment.
    // Body: { "contexto": { personas, edades[], movilidad[], mascotas[] }, "criterios": { "seguridad": "alta" },
    //         "puntos": { "alquilerMaximo": "800000", "lugares": [] } }
    public class CriterioUsuarioRequest
    {
        [Required(ErrorMessage = "El contexto es obligatorio.")]
        public ContextoRequest Contexto { get; set; } = null!;

        // Clave: id del catalogo de criterios; valor: baja|media|alta
        [Required(ErrorMessage = "Los criterios son obligatorios.")]
        public Dictionary<string, string?> Criterios { get; set; } = null!;

        [Required(ErrorMessage = "Los puntos son obligatorios.")]
        public PuntosRequest Puntos { get; set; } = null!;
    }
}
