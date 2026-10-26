using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // Un lugar elegido en el mapa del wizard (fila de app.puntos_interes).
    public class LugarRequest
    {
        [Required(ErrorMessage = "El nombre del lugar es obligatorio.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "El nombre del lugar debe tener entre 1 y 100 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "La dirección no puede superar 200 caracteres.")]
        public string? Direccion { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [StringLength(30)]
        public string? Tipo { get; set; }

        [StringLength(50)]
        public string? QuienVa { get; set; }

        [StringLength(20)]
        public string? Frecuencia { get; set; }

        [StringLength(20)]
        public string? Prioridad { get; set; }

        [StringLength(10)]
        public string? TiempoMax { get; set; }
    }
}
