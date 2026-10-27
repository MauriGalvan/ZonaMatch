using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IPasswordResetRepository
{
    Task<int> CountCreatedSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // Saves the new password and marks every pending link of the user as used, in one transaction
    Task ResetPasswordAsync(PasswordResetToken token, string passwordHash, DateTimeOffset now, CancellationToken cancellationToken = default);
}
