namespace ZonaMatch.Domain.Entities
{
    // A one-time link to choose a new password. Only the SHA-256 hash of the token is stored:
    // whoever reads the database cannot use it.
    public class PasswordResetToken
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }

        public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;
    }
}
