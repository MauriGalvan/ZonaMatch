using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // Bloque "puntos" del wizard: { lugares[], alquilerMaximo }.
    public class PuntosRequest
    {
        // Tope de alquiler tipeado por el usuario, verbatim (ej: "800000")
        [StringLength(30, ErrorMessage = "El alquiler máximo no puede superar 30 caracteres.")]
        public string? AlquilerMaximo { get; set; }

        public List<LugarRequest> Lugares { get; set; } = [];
    }
}
