namespace ZonaMatch.Domain.Entities
{
    // Registered user of the site (app.usuarios table).
    public class User
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Stored trimmed and lowercase, so the unique index is case-insensitive
        public string Email { get; set; } = string.Empty;

        // Output of IPasswordHasher (salted PBKDF2). The plain password is never stored.
        public string PasswordHash { get; set; } = string.Empty;

        public DateOnly DateOfBirth { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // Site administrator: can approve or reject community contributions directly
        public bool IsAdministrator { get; set; }

        // Investor profile access (InvestorAccess values): null until the user asks for it. For now an administrator
        // grants it; later it will follow a subscription.
        public string? InvestorAccess { get; set; }

        public DateTimeOffset? InvestorAccessRequestedAt { get; set; }

        public DateTimeOffset? InvestorAccessGrantedAt { get; set; }

        public bool HasInvestorAccess => InvestorAccess == Entities.InvestorAccess.Granted;
    }

    // Stored values of User.InvestorAccess.
    public static class InvestorAccess
    {
        public const string Requested = "solicitado";
        public const string Granted = "habilitado";
    }
}
