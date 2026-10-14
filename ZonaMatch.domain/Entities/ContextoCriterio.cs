namespace ZonaMatch.Domain.Entities
{
    // Fila de la relacion N:N contexto <-> criterio (tabla app.contexto_criterios).
    // La prioridad es el "peso" del criterio dentro de este analisis: baja|media|alta.
    public class ContextoCriterio
    {
        public Guid ContextoId { get; set; }

        public Contexto? Contexto { get; set; }

        public string CriterioId { get; set; } = string.Empty;

        public Criterio? Criterio { get; set; }

        public string Prioridad { get; set; } = "media";
    }
}
