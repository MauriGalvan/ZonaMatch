using System.ComponentModel.DataAnnotations;

namespace ZonaMatch.Application.DTOs
{
    // Bloque "puntos" del wizard: { lugares[], alquilerMaximo, operacion, compraMaximaUsd, tipoVivienda }.
    public class PuntosRequest
    {
        // Tope de gasto mensual tipeado por el usuario, verbatim (ej: "800000"): alquiler mas expensas, o solo
        // expensas si compra
        [StringLength(30, ErrorMessage = "El alquiler máximo no puede superar 30 caracteres.")]
        public string? AlquilerMaximo { get; set; }

        public List<LugarRequest> Lugares { get; set; } = [];

        // alquiler | compra; sin dato, alquiler
        [StringLength(10)]
        public string? Operacion { get; set; }

        // Precio maximo de compra en dolares (solo si compra)
        [Range(typeof(decimal), "0", "100000000")]
        public decimal? CompraMaximaUsd { get; set; }

        // apartment | house | ph; sin dato, todos los tipos
        [StringLength(20)]
        public string? TipoVivienda { get; set; }

        // Superficie que busca en m2 (opcional): los precios se estiman por m2
        [Range(0, 2000, ErrorMessage = "La superficie debe estar entre 10 y 2.000 m².")]
        public int? SuperficieM2 { get; set; }
    }
}
