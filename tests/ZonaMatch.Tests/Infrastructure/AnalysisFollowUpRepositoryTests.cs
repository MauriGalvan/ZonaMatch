using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Infrastructure.Data;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Tests.Infrastructure;

public sealed class AnalysisFollowUpRepositoryTests
{
    [Fact]
    public async Task SaveChanges_TranslatesAStaleWriteIntoAConflict()
    {
        var context = CreateContext();
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Stale follow-up"));

        await Assert.ThrowsAsync<ConflictException>(() => new AnalysisFollowUpRepository(context.Object).SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_TranslatesADuplicateInvitationIntoAConflict()
    {
        var context = CreateContext();
        var duplicate = new PostgresException("Duplicate invitation", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Save failed", duplicate));

        await Assert.ThrowsAsync<ConflictException>(() => new AnalysisFollowUpRepository(context.Object).SaveChangesAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_DoesNotMisreportOtherDatabaseErrorsAsConflicts(bool postgresError)
    {
        var context = CreateContext();
        var failure = new DbUpdateException("Save failed", postgresError
            ? new PostgresException("Foreign key violation", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation)
            : new InvalidOperationException("Database unavailable"));
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => new AnalysisFollowUpRepository(context.Object).SaveChangesAsync());

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task SaveChanges_ForwardsCancellationWithoutConvertingItIntoAConflict()
    {
        var context = CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.Setup(db => db.SaveChangesAsync(cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => new AnalysisFollowUpRepository(context.Object)
            .SaveChangesAsync(cancellation.Token));
        context.Verify(db => db.SaveChangesAsync(cancellation.Token), Times.Once);
    }

    private static Mock<ZonaMatchDbContext> CreateContext() =>
        new(new DbContextOptionsBuilder<ZonaMatchDbContext>().Options);
}
