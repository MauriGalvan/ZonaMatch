namespace ZonaMatch.Domain.Entities
{
    // Join row for the context-to-criterion relationship (app.contexto_criterios table).
    // Priority is the criterion's weight in this analysis: baja | media | alta.
    public class ContextCriterion
    {
        public Guid ContextoId { get; set; }

        public Context? Context { get; set; }

        public string CriterioId { get; set; } = string.Empty;

        public Criterion? Criterion { get; set; }

        public string Prioridad { get; set; } = "media";
    }
}
