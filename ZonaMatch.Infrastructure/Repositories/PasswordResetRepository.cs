using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.PasswordResets;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

public sealed class PasswordResetRepository(ZonaMatchDbContext dbContext) : IPasswordResetRepository
{
    public Task<int> CountCreatedSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        dbContext.PasswordResetTokens.CountAsync(token => token.UserId == userId && token.CreatedAt >= since, cancellationToken);

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        dbContext.PasswordResetTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.PasswordResetTokens.AsNoTracking().FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task ResetPasswordAsync(PasswordResetToken token, string passwordHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // The context retries transient failures, so the transaction runs inside its execution strategy
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var changed = await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            // Only if the link is still unused: two requests with the same link cannot both change the password
            var claimed = await dbContext.PasswordResetTokens
                .Where(t => t.Id == token.Id && t.UsedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, now), ct);
            if (claimed == 0)
                return false;

            await dbContext.Usuarios
                .Where(user => user.Id == token.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, passwordHash), ct);
            await dbContext.PasswordResetTokens
                .Where(t => t.UserId == token.UserId && t.UsedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, now), ct);

            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);

        if (!changed)
            throw new BadRequestException(CompletePasswordReset.InvalidLinkMessage);
    }
}
