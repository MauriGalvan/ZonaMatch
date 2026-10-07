using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    public enum TipoAporte
    {
        // A place that is missing from the map
        PuntoNuevo,
        // Something wrong in a place of the map
        Correccion
    }

    public enum EstadoAporte
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public enum MotivoCorreccion
    {
        Cerro,
        Nombre,
        Ubicacion,
        Categoria,
        Otro
    }

    // A neighbor's contribution to the map of a zone (table app.aportes). It stays pending until other
    // neighbors validate it: approved points and corrections are applied to the points of interest.
    public class Aporte
    {
        // Difference between confirmations and rejections that resolves a pending contribution
        public const int DiferenciaParaResolver = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public TipoAporte Tipo { get; set; }
        public EstadoAporte Estado { get; set; } = EstadoAporte.Pendiente;

        // New point: its data. Correction: the proposed value (Nombre, Categoria or Ubicacion, by Motivo).
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public Point? Ubicacion { get; set; }

        // New point only
        public string? Horario { get; set; }
        public string? Direccion { get; set; }
        public bool ViveOTrabajaEnZona { get; set; }

        // Correction only: the corrected place (OSM id like "n123", or "a<id>" for a neighbor's point)
        public string? PuntoInteresId { get; set; }
        public string? PuntoInteresNombre { get; set; }
        public MotivoCorreccion? Motivo { get; set; }
        public string? Comentario { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset? FechaResolucion { get; set; }

        public List<AporteValidacion> Validaciones { get; set; } = [];

        public int Confirmaciones => Validaciones.Count(v => v.Confirma);
        public int Rechazos => Validaciones.Count(v => !v.Confirma);

        // Records (or changes) the vote of a user and resolves the contribution when the difference is reached.
        // The caller checks that the contribution is pending and that the user is not its author.
        public void Validar(Guid usuarioId, bool confirma, DateTimeOffset ahora)
        {
            var existente = Validaciones.FirstOrDefault(v => v.UsuarioId == usuarioId);
            if (existente is null)
                Validaciones.Add(new AporteValidacion { AporteId = Id, UsuarioId = usuarioId, Confirma = confirma, Fecha = ahora });
            else
            {
                existente.Confirma = confirma;
                existente.Fecha = ahora;
            }

            var diferencia = Confirmaciones - Rechazos;
            if (Math.Abs(diferencia) >= DiferenciaParaResolver)
            {
                Estado = diferencia > 0 ? EstadoAporte.Aprobado : EstadoAporte.Rechazado;
                FechaResolucion = ahora;
            }
        }
    }

    // Vote of a neighbor on a contribution (table app.aportes_validaciones)
    public class AporteValidacion
    {
        public Guid AporteId { get; set; }
        public Guid UsuarioId { get; set; }
        public bool Confirma { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
