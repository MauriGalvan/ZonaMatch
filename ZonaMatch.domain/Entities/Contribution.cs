using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    public enum ContributionType
    {
        // A place missing from the map.
        PuntoNuevo,
        // Something is wrong with a place on the map.
        Correccion
    }

    public enum ContributionStatus
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public enum CorrectionReason
    {
        Cerro,
        Nombre,
        Ubicacion,
        Categoria,
        Otro
    }

    // A community contribution to a zone map (app.aportes table). It remains pending until other community
    // members validate it; approved points and corrections are applied to points of interest.
    public class Contribution
    {
        // Difference between confirmations and rejections needed to resolve a pending contribution.
        public const int DiferenciaParaResolver = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public User? User { get; set; }

        public ContributionType Tipo { get; set; }
        public ContributionStatus Estado { get; set; } = ContributionStatus.Pendiente;

        // For a new point, its data. For a correction, the proposed value (Name, Category, or Location, based on Reason).
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public Point? Ubicacion { get; set; }

        // Used only for a new point.
        public string? Horario { get; set; }
        public string? Direccion { get; set; }
        public bool ViveOTrabajaEnZona { get; set; }

        // Used only for a correction: the corrected place (an OSM ID such as "n123", or "a<id>" for a community point).
        public string? PuntoInteresId { get; set; }
        public string? PuntoInteresNombre { get; set; }
        public CorrectionReason? Motivo { get; set; }
        public string? Comentario { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset? FechaResolucion { get; set; }

        // Site administrator who resolved it directly; null when the community votes resolved it
        public Guid? ResueltoPorId { get; set; }

        public List<ContributionVote> Validaciones { get; set; } = [];

        public int Confirmaciones => Validaciones.Count(v => v.Confirma);
        public int Rechazos => Validaciones.Count(v => !v.Confirma);

        // Record or change a user's vote and resolve the contribution when the vote threshold is reached.
        // The caller verifies that the contribution is pending and the voter is not its author.
        public void Validar(Guid usuarioId, bool confirma, DateTimeOffset ahora)
        {
            var existente = Validaciones.FirstOrDefault(v => v.UsuarioId == usuarioId);
            if (existente is null)
                Validaciones.Add(new ContributionVote { AporteId = Id, UsuarioId = usuarioId, Confirma = confirma, Fecha = ahora });
            else
            {
                existente.Confirma = confirma;
                existente.Fecha = ahora;
            }

            var diferencia = Confirmaciones - Rechazos;
            if (Math.Abs(diferencia) >= DiferenciaParaResolver)
            {
                Estado = diferencia > 0 ? ContributionStatus.Aprobado : ContributionStatus.Rechazado;
                FechaResolucion = ahora;
            }
        }

        // A site administrator approves or rejects it without waiting for the votes.
        // The caller checks that the contribution is pending.
        public void Resolve(Guid administratorId, bool approve, DateTimeOffset now)
        {
            Estado = approve ? ContributionStatus.Aprobado : ContributionStatus.Rechazado;
            FechaResolucion = now;
            ResueltoPorId = administratorId;
        }
    }

    // A community member's vote on a contribution (app.aportes_validaciones table).
    public class ContributionVote
    {
        public Guid AporteId { get; set; }
        public Guid UsuarioId { get; set; }
        public bool Confirma { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
