namespace ZonaMatch.Application.DTOs.Questions.Responses;

// Answers are ordered from oldest to newest. IsMine is false for anonymous requests.
public sealed record QuestionResponse(
    Guid Id,
    string Text,
    string Initials,
    DateTimeOffset CreatedAt,
    bool IsMine,
    IReadOnlyList<AnswerResponse> Answers);
