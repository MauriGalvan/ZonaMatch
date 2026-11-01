using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ZonaMatch.Application.DTOs
{
    // POST /Puntajes: automatic ranking around geocoded places, with an optional initial zone and added zones.
    // Visitors and signed-in users provide the scoring context in the request.
    public class CalcularPuntajesRequest
    {
        [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
        public List<string> ZonasAgregadas { get; set; } = [];

        [StringLength(100)]
        public string? ZonaInicial { get; set; }

        // Key: criterion ID (seguridad, movilidad, espacios-verdes, educacion, salud); value: baja|media|alta.
        public Dictionary<string, string?> Criterios { get; set; } = [];

        // Visitors: up to 3 (PuntajesController); signed-in users: up to 15
        [MaxLength(15, ErrorMessage = "Se pueden medir hasta 15 lugares.")]
        public List<LugarPuntajeRequest> Lugares { get; set; } = [];

        // Maximum monthly expense in Argentine pesos, as entered by the user (for example, "800000"): rent plus
        // building expenses when renting, or building expenses only when purchasing.
        [StringLength(30)]
        public string? AlquilerMaximo { get; set; }

        // alquiler | compra; defaults to alquiler when omitted.
        [StringLength(10)]
        public string? Operacion { get; set; }

        // Maximum purchase price in US dollars (only when purchasing).
        [Range(typeof(decimal), "0", "100000000")]
        public decimal? CompraMaximaUsd { get; set; }

        // apartment | house | ph; all housing types when omitted.
        [StringLength(20)]
        public string? TipoVivienda { get; set; }

        // Desired area in square meters: prices are estimated per square meter; zone averages are used when omitted.
        [Range(0, 2000, ErrorMessage = "La superficie debe estar entre 10 y 2.000 m².")]
        public int? SuperficieM2 { get; set; }

        // Mobility options (walking, public transit, car, cycling) determine how trips are measured.
        public List<string> Movilidad { get; set; } = [];

        // Starting point chosen by the user inside a zone (a point on the map instead of its center): the trips to
        // the places, the time limits and the score of that zone are measured from there.
        [MaxLength(10, ErrorMessage = "Se pueden elegir hasta 10 puntos de partida.")]
        public List<OrigenZonaRequest> OrigenesZona { get; set; } = [];
    }

    public class OrigenZonaRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Slug { get; set; } = string.Empty;

        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Range(-180, 180)]
        public double Longitude { get; set; }
    }

    public class LugarPuntajeRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Nombre { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        // Muy importante | Alta | Media | Baja.
        [StringLength(20)]
        public string? Prioridad { get; set; }

        // Example: "45 min".
        [StringLength(10)]
        public string? TiempoMax { get; set; }
    }

    public record PuntajesDto(
        [property: JsonPropertyName("modoViaje")] string TravelMode,
        IReadOnlyList<PuntajeZonaDto> Zonas,
        IReadOnlyList<PuntajeZonaDto> Cercanas);

    // Score is null when no criterion has data for the zone.
    public record PuntajeZonaDto(
        string Slug,
        string Nombre,
        int? Puntaje,
        IReadOnlyList<ComponentePuntajeDto> Componentes,
        IReadOnlyList<LimitePuntajeDto> Limites,
        IReadOnlyList<TiempoLugarDto> Lugares,
        AlquilerZonaDto? Alquiler,
        CompraZonaDto? Compra = null);

    public record ComponentePuntajeDto(string Criterio, string Prioridad, int? Puntaje, string Detalle);

    // IsMet is null when there is not enough data to determine the result.
    public record LimitePuntajeDto(string Tipo, bool? Cumple, string Detalle);

    // Route distance, not straight-line distance. Null when the complete route is unknown.
    public record TiempoLugarDto(
        string Nombre,
        int? Minutos,
        int MaximoMinutos,
        bool Estimado,
        double? DistanciaKm = null);

    // Expenses is the monthly average in Argentine pesos (null when unavailable). MonthlyCost is rent plus expenses.
    // PricePerM2 is the average rent per square meter. When an area is requested, SurfaceM2 and Estimated provide the
    // rent estimate for that area, and MonthlyCost uses that estimate.
    public record AlquilerZonaDto(
        decimal Promedio,
        int PropiedadesDisponibles,
        decimal? Expensas = null,
        decimal? CostoMensual = null,
        decimal? PrecioM2 = null,
        int? SuperficieM2 = null,
        decimal? Estimado = null);

    // AverageUsd is the average sale price in US dollars. Expenses is the monthly average in Argentine pesos (null
    // when unavailable). PricePerM2Usd is the average sale price per square meter. When an area is requested, SurfaceM2
    // and EstimatedUsd provide the estimated price for that area.
    public record CompraZonaDto(
        decimal PromedioUsd,
        int PropiedadesDisponibles,
        decimal? Expensas = null,
        decimal? PrecioM2Usd = null,
        int? SuperficieM2 = null,
        decimal? EstimadoUsd = null);
}
