namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/questions/deleted: questions an administrator deleted, most recently deleted first.
public sealed record AdminDeletedQuestionResponse(
    Guid Id,
    string ZoneSlug,
    string ZoneName,
    string Text,
    string AuthorEmail,
    DateTimeOffset AskedAt,
    int AnswerCount,
    DateTimeOffset DeletedAt,
    string DeletedByEmail);
