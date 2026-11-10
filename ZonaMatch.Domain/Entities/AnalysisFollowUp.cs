namespace ZonaMatch.Domain.Entities;

// One follow-up per original saved analysis; versions share their original's follow-up.
public class AnalysisFollowUp
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid AnalysisId { get; set; }
    public Analysis Analysis { get; set; } = null!;
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset? PostponedUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Outcome { get; set; }
    public int? Helpfulness { get; set; }
    public string? Comment { get; set; }
}
