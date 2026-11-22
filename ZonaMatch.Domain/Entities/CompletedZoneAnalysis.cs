namespace ZonaMatch.Domain.Entities;

// Only successful authenticated rankings are recorded. A repeated request is not new experience.
public sealed class CompletedZoneAnalysis
{
    public Guid UserId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
