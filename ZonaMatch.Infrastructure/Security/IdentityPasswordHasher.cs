using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Infrastructure.Security
{
    // Uses the hasher of ASP.NET Core Identity (PBKDF2-HMAC-SHA512, random 128-bit salt per password)
    // without pulling in the rest of Identity. The iteration count is stored inside each hash, so it can be
    // raised later without invalidating existing passwords.
    public class IdentityPasswordHasher : IPasswordHasher
    {
        // OWASP recommendation for PBKDF2-HMAC-SHA512
        private const int IterationCount = 210_000;

        private readonly PasswordHasher<Usuario> _hasher =
            new(Options.Create(new PasswordHasherOptions { IterationCount = IterationCount }));

        // Valid hash of a random password, used to spend the same time when the user does not exist
        private readonly string _dummyHash;

        public IdentityPasswordHasher() => _dummyHash = Hash(Guid.NewGuid().ToString("N"));

        // PasswordHasher<T> ignores the user instance; null is the documented way to hash without one
        public string Hash(string password) => _hasher.HashPassword(null!, password);

        public bool Verify(string? hash, string password)
        {
            var result = _hasher.VerifyHashedPassword(null!, hash ?? _dummyHash, password);
            return hash is not null && result != PasswordVerificationResult.Failed;
        }
    }
}
