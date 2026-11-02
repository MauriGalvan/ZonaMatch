using NetTopologySuite.Geometries;

namespace ZonaMatch.Domain.Entities
{
    public enum ContributionType
    {
        // Un lugar que falta en el mapa
        PuntoNuevo,
        // Algo mal en un lugar del mapa
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

    // Contribution de un vecino al mapa de una zona (tabla app.aportes). Queda pendiente hasta que otros vecinos
    // lo validan: los puntos y correcciones aprobados se aplican a los puntos de interes.
    public class Contribution
    {
        // Diferencia entre confirmaciones y rechazos que resuelve un aporte pendiente
        public const int DiferenciaParaResolver = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public User? User { get; set; }

        public ContributionType Tipo { get; set; }
        public ContributionStatus Estado { get; set; } = ContributionStatus.Pendiente;

        // Punto nuevo: sus datos. Correccion: el valor propuesto (Nombre, Categoria o Ubicacion, segun el Motivo).
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public Point? Ubicacion { get; set; }

        // Solo punto nuevo
        public string? Horario { get; set; }
        public string? Direccion { get; set; }
        public bool ViveOTrabajaEnZona { get; set; }

        // Solo correccion: el lugar corregido (id de OSM como "n123", o "a<id>" para un punto de vecinos)
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

        // Registra (o cambia) el voto de un usuario y resuelve el aporte cuando se llega a la diferencia.
        // Quien lo llama verifica que el aporte este pendiente y que el usuario no sea su autor.
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

    // Voto de un vecino sobre un aporte (tabla app.aportes_validaciones)
    public class ContributionVote
    {
        public Guid AporteId { get; set; }
        public Guid UsuarioId { get; set; }
        public bool Confirma { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
