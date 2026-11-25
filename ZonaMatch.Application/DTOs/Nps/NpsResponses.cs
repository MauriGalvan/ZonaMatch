namespace ZonaMatch.Application.DTOs.Nps;

public sealed record NpsInvitationResponse(Guid Id);
public sealed record NpsAnswerResponse(Guid Id, DateTimeOffset CompletedAt, int Score, string? Comment);
public sealed record NpsSummary(int Total, int Promoters, int Passives, int Detractors, double? Nps);
public sealed record AdminNpsResponse(IReadOnlyList<NpsAnswerResponse> Items, int Page, int PageSize, NpsSummary Summary);
