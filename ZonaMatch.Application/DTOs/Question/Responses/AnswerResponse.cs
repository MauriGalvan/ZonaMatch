namespace ZonaMatch.Application.DTOs.Question.Responses;

public sealed record AnswerResponse(Guid Id, string Text, string Initials, DateTimeOffset CreatedAt, bool IsMine);
