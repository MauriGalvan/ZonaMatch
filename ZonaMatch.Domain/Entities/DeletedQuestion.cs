namespace ZonaMatch.Domain.Entities
{
    // A community question an administrator deleted (app.deleted_questions table), kept for the moderation history. The
    // question and its answers are gone, so this keeps a copy of what was deleted and who deleted it.
    public class DeletedQuestion
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // The deleted question's id; it no longer exists in app.preguntas.
        public Guid QuestionId { get; set; }

        public string ZoneSlug { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        // A copy, so the history keeps it even if the account is deleted later.
        public string AuthorEmail { get; set; } = string.Empty;

        public DateTimeOffset AskedAt { get; set; }

        // Answers deleted with the question.
        public int AnswerCount { get; set; }

        public DateTimeOffset DeletedAt { get; set; }

        public string DeletedByEmail { get; set; } = string.Empty;
    }
}
