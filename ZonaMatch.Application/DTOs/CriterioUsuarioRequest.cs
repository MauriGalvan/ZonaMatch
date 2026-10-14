using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // Criterio completo de un usuario: el perfil (PUT /Perfil) o el ajuste de una version de analisis.
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
