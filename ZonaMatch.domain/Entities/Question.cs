namespace ZonaMatch.Domain.Entities
{
    // A question for a zone's community (app.preguntas table).
    public class Question
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public User? User { get; set; }

        public string Texto { get; set; } = string.Empty;

        public DateTimeOffset FechaCreacion { get; set; }

        public List<Answer> Respuestas { get; set; } = [];
    }

    // An answer to a question (app.respuestas table).
    public class Answer
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid PreguntaId { get; set; }

        public Guid UsuarioId { get; set; }
        public User? User { get; set; }

        public string Texto { get; set; } = string.Empty;

        public DateTimeOffset FechaCreacion { get; set; }
    }
}
