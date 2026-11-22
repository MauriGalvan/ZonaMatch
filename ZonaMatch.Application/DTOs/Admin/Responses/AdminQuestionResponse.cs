namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/questions: the latest questions of every zone, newest first, with their answers (oldest first).
public sealed record AdminQuestionResponse(
    Guid Id,
    string ZoneSlug,
    string ZoneName,
    string AuthorEmail,
    string Text,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminAnswerResponse> Answers);

public sealed record AdminAnswerResponse(Guid Id, string AuthorEmail, string Text, DateTimeOffset CreatedAt);
