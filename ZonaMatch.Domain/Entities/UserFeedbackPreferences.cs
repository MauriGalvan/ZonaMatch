namespace ZonaMatch.Domain.Entities;

// Survey preferences belong to the account, so they also apply on another device.
public class UserFeedbackPreferences
{
    public Guid UserId { get; set; }
    public bool SurveysEnabled { get; set; } = true;
    public DateTimeOffset? NextInvitationAt { get; set; }
}
