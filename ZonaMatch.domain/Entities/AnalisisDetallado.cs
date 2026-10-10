namespace ZonaMatch.Domain.Entities
{
    // Detailed analysis saved by a registered user (table app.analisis_detallado).
    // The wizard payload (contexto, criterios, puntos) is stored as jsonb: the shapes are
    // heterogeneous string unions that evolve with the frontend, and no server-side query needs them.
    public class AnalisisDetallado
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }

        // User-chosen name shown in the saved list, e.g. "Familia Palermo"
        public string Nombre { get; set; } = string.Empty;

        // jsonb: { personas, edades[], movilidad[], mascotas[] }
        public string ContextoJson { get; set; } = "{}";

        // jsonb: { "seguridad": "alta", ... } — only known criterio ids with baja|media|alta
        public string CriteriosJson { get; set; } = "{}";

        // jsonb: { lugares[], alquilerMaximo }
        public string PuntosJson { get; set; } = "{}";

        public DateTimeOffset FechaCreacion { get; set; }

        public DateTimeOffset FechaActualizacion { get; set; }
    }
}
