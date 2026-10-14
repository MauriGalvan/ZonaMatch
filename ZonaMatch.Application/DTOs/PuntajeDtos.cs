using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // POST /Puntajes — puntaje de las zonas de un analisis (y de las cercanas) para un criterio.
    // Lo usan visitantes y usuarios: el criterio viaja en el pedido, no se lee del perfil.
    public class CalcularPuntajesRequest
    {
        [Required(ErrorMessage = "Las zonas son obligatorias.")]
        [MinLength(1, ErrorMessage = "Elegí al menos una zona.")]
        [MaxLength(10, ErrorMessage = "Se pueden comparar hasta 10 zonas.")]
        public List<string> Zonas { get; set; } = [];

        // Clave: id del criterio (seguridad, movilidad, espacios-verdes, educacion, salud); valor: baja|media|alta
        public Dictionary<string, string?> Criterios { get; set; } = [];

        [MaxLength(10, ErrorMessage = "Se pueden medir hasta 10 lugares.")]
        public List<LugarPuntajeRequest> Lugares { get; set; } = [];

        // Tope de alquiler mensual en pesos, como lo tipeo el usuario (ej: "800000")
        [StringLength(30)]
        public string? AlquilerMaximo { get; set; }

        // Movilidad del hogar (a-pie, transporte-publico, vehiculo, bici): define como se miden los viajes
        public List<string> Movilidad { get; set; } = [];

        public bool IncluirCercanas { get; set; } = true;
    }

    public class LugarPuntajeRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Nombre { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        // Muy importante | Alta | Media | Baja
        [StringLength(20)]
        public string? Prioridad { get; set; }

        // "45 min"
        [StringLength(10)]
        public string? TiempoMax { get; set; }
    }

    public record PuntajesDto(
        string ModoViaje,
        IReadOnlyList<PuntajeZonaDto> Zonas,
        IReadOnlyList<PuntajeZonaDto> Cercanas);

    // Puntaje null si ningun criterio tiene datos para la zona
    public record PuntajeZonaDto(
        string Slug,
        string Nombre,
        int? Puntaje,
        IReadOnlyList<ComponentePuntajeDto> Componentes,
        IReadOnlyList<LimitePuntajeDto> Limites,
        IReadOnlyList<TiempoLugarDto> Lugares,
        AlquilerZonaDto? Alquiler);

    public record ComponentePuntajeDto(string Criterio, string Prioridad, int? Puntaje, string Detalle);

    // Cumple null si no hay datos para saberlo
    public record LimitePuntajeDto(string Tipo, bool? Cumple, string Detalle);

    public record TiempoLugarDto(string Nombre, int? Minutos, int MaximoMinutos, bool Estimado);

    public record AlquilerZonaDto(decimal Promedio, int PropiedadesDisponibles);
}
