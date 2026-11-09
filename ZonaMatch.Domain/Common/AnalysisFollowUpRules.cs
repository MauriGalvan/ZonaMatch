namespace ZonaMatch.Domain.Common;

public static class AnalysisFollowUpRules
{
    public const int MinimumAnalysisAgeDays = 14;
    public const int InvitationIntervalDays = 14;
    public const int PostponeDays = 30;
    public const int MaximumCommentLength = 2000;

    public static bool IsValidOutcome(string? outcome) => outcome is
        "still-searching" or "visited-recommended-zone" or "chose-recommended-zone"
        or "chose-another-zone" or "stopped-searching";
}
