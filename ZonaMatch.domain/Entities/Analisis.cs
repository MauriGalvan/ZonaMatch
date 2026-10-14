namespace ZonaMatch.Domain.Entities
{
    // Analisis guardado de un usuario (tabla app.analisis): las zonas que comparo y el criterio con el que
    // se calculo, copiado del perfil al guardarlo. Una version es un analisis derivado de otro, con el
    // criterio ajustado solo para ese caso: AnalisisOrigenId apunta al original.
    public class Analisis
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }

        // Nombre que el usuario ve en su listado, ej: "Cerca del trabajo"
        public string Nombre { get; set; } = string.Empty;

        // Criterio con el que se calculo (Contexto de tipo analisis, propio de este analisis)
        public Guid ContextoId { get; set; }

        public Contexto Contexto { get; set; } = null!;

        // null en los originales; en una version, el analisis del que deriva (siempre un original)
        public Guid? AnalisisOrigenId { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }

        public DateTimeOffset FechaActualizacion { get; set; }

        // Filas de app.analisis_zonas, en el orden en que el usuario las eligio
        public ICollection<AnalisisZona> Zonas { get; set; } = new List<AnalisisZona>();
    }
}
