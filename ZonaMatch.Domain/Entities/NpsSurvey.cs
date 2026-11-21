namespace ZonaMatch.Domain.Entities;

public sealed class NpsSurvey
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? PostponedAt { get; set; }
    public int? Score { get; set; }
    public string? Comment { get; set; }
}
