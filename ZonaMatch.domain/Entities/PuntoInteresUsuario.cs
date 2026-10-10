using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Entities
{
    // Lugar que un usuario marca como importante para su mudanza (trabajo, colegio, casa de un amigo...),
    // tabla app.puntos_interes_usuario. Es privado de su dueño y distinto de los puntos del mapa (OSM y aportes
    // de vecinos) que lee PuntoInteresRepository. Un usuario puede tener muchos.
    public class PuntoInteresUsuario : IUbicacionPunto
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public Guid TagId { get; set; }
        public TagPuntoInteres Tag { get; set; } = null!;

        // Nombre opcional que le pone el usuario ("Oficina", "Lo de Juan")
        public string? Alias { get; set; }

        // geometry(Point,4326): el punto elegido en el mapa
        public Point Ubicacion { get; set; } = null!;

        // Resueltos por el servidor a partir de la ubicacion al momento del alta
        public string? Direccion { get; set; }
        public string? Barrio { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
    }
}
