namespace ZonaMatch.Domain.Entities
{
    // Registered user of the site (table app.usuarios)
    public class Usuario
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Stored trimmed and lowercase, so the unique index is case-insensitive
        public string Email { get; set; } = string.Empty;

        // Output of IPasswordHasher (salted PBKDF2). The plain password is never stored.
        public string PasswordHash { get; set; } = string.Empty;

        public DateOnly FechaNacimiento { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
    }
}
