namespace ZonaMatch.Domain.Entities
{
    // A question for a zone's community (app.preguntas table).
    public class Question
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public string ZoneSlug { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public List<Answer> Answers { get; set; } = [];
    }

    // An answer to a question (app.respuestas table).
    public class Answer
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid QuestionId { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
