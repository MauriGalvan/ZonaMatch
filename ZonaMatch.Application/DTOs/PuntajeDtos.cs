using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // POST /Puntajes — automatic ranking around geocoded places, with an optional initial zone and added zones.
    // Visitors and signed-in users send the scoring context in the request.
    public class CalcularPuntajesRequest
    {
        [MaxLength(5, ErrorMessage = "Se pueden agregar hasta 5 zonas después del ranking.")]
        public List<string> ZonasAgregadas { get; set; } = [];

        [StringLength(100)]
        public string? ZonaInicial { get; set; }

        // Clave: id del criterio (seguridad, movilidad, espacios-verdes, educacion, salud); valor: baja|media|alta
        public Dictionary<string, string?> Criterios { get; set; } = [];

        // Visitors: up to 3 (PuntajesController); signed-in users: up to 15
        [MaxLength(15, ErrorMessage = "Se pueden medir hasta 15 lugares.")]
        public List<LugarPuntajeRequest> Lugares { get; set; } = [];

        // Tope de gasto mensual en pesos, como lo tipeo el usuario (ej: "800000"): alquiler mas expensas, o solo
        // expensas si compra
        [StringLength(30)]
        public string? AlquilerMaximo { get; set; }

        // alquiler | compra; sin dato, alquiler
        [StringLength(10)]
        public string? Operacion { get; set; }

        // Precio maximo de compra en dolares (solo si compra)
        [Range(typeof(decimal), "0", "100000000")]
        public decimal? CompraMaximaUsd { get; set; }

        // apartment | house | ph; sin dato, todos los tipos
        [StringLength(20)]
        public string? TipoVivienda { get; set; }

        // Superficie que busca en m2: los precios se estiman por m2; sin dato, los promedios de cada zona
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
        AlquilerZonaDto? Alquiler,
        CompraZonaDto? Compra = null);

    public record ComponentePuntajeDto(string Criterio, string Prioridad, int? Puntaje, string Detalle);

    // Cumple null si no hay datos para saberlo
    public record LimitePuntajeDto(string Tipo, bool? Cumple, string Detalle);

    // Distancia del recorrido, no la separacion en linea recta. Null si no se conoce el recorrido completo.
    public record TiempoLugarDto(
        string Nombre,
        int? Minutos,
        int MaximoMinutos,
        bool Estimado,
        double? DistanciaKm = null);

    // Expensas: promedio mensual en pesos (null sin dato). CostoMensual: alquiler mas expensas.
    // PrecioM2: alquiler promedio por m2. Con superficie pedida, SuperficieM2 y Estimado (alquiler para esa superficie);
    // CostoMensual usa ese estimado.
    public record AlquilerZonaDto(
        decimal Promedio,
        int PropiedadesDisponibles,
        decimal? Expensas = null,
        decimal? CostoMensual = null,
        decimal? PrecioM2 = null,
        int? SuperficieM2 = null,
        decimal? Estimado = null);

    // PromedioUsd: precio de venta promedio en dolares. Expensas: promedio mensual en pesos (null sin dato).
    // PrecioM2Usd: venta promedio por m2. Con superficie pedida, SuperficieM2 y EstimadoUsd (precio para esa superficie).
    public record CompraZonaDto(
        decimal PromedioUsd,
        int PropiedadesDisponibles,
        decimal? Expensas = null,
        decimal? PrecioM2Usd = null,
        int? SuperficieM2 = null,
        decimal? EstimadoUsd = null);
}
