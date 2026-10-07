namespace ZonaMatch.Domain.Entities
{
    // Question to the neighbors of a zone (table app.preguntas)
    public class Pregunta
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public string Texto { get; set; } = string.Empty;

        public DateTimeOffset FechaCreacion { get; set; }

        public List<Respuesta> Respuestas { get; set; } = [];
    }

    // Answer to a question (table app.respuestas)
    public class Respuesta
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid PreguntaId { get; set; }

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public string Texto { get; set; } = string.Empty;

        public DateTimeOffset FechaCreacion { get; set; }
    }
}
