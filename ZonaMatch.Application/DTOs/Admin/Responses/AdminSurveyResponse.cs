namespace ZonaMatch.Application.DTOs.Admin.Responses;

public sealed record AdminSurveyResponse(
    IReadOnlyList<AdminSurveyAnswer> Items, int Page, int PageSize, AdminSurveySummary Summary);

public sealed record AdminSurveyAnswer(
    Guid Id, Guid AnalysisId, DateTimeOffset CompletedAt, string Outcome, int Helpfulness, string? Comment);

// Summary always covers all matching answers, rather than just the current page.
public sealed record AdminSurveySummary(
    int Total, double? AverageHelpfulness, IReadOnlyList<AdminSurveyOutcomeCount> Outcomes);

public sealed record AdminSurveyOutcomeCount(string Outcome, int Count);
