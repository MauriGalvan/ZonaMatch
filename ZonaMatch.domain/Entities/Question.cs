namespace ZonaMatch.Domain.Entities
{
    // Question a los vecinos de una zona (tabla app.preguntas)
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

    // Answer a una pregunta (tabla app.respuestas)
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
