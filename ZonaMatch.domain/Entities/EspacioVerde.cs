using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    public class EspacioVerde : IUbicacionPunto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Ej: "Plaza", "Parque", "Reserva natural"
        public string? Tipo { get; set; }
        public double? SuperficieM2 { get; set; }

        public Point Ubicacion { get; set; } = null!;
    }
}
