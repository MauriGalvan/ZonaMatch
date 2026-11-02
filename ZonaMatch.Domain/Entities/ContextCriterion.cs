namespace ZonaMatch.Domain.Entities
{
    // Join row for the context-to-criterion relationship (app.contexto_criterios table).
    // Priority is the criterion's weight in this analysis: baja | media | alta.
    public class ContextCriterion
    {
        public Guid ContextId { get; set; }

        public Context? Context { get; set; }

        public string CriterionId { get; set; } = string.Empty;

        public Criterion? Criterion { get; set; }

        public string Priority { get; set; } = "media";
    }
}
