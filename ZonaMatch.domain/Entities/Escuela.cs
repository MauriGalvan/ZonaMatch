using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    public class Escuela : IUbicacionPunto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Ej: "Inicial", "Primario", "Secundario"
        public string? Nivel { get; set; }

        // Ej: "Publica", "Privada"
        public string? Gestion { get; set; }
        public string? Direccion { get; set; }

        public Point Ubicacion { get; set; } = null!;
    }
}
