using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AnalysisFollowUpRepositoryTests(PostgreSqlDatabaseFixture database)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChangesAsync_WhenPreferencesAreStale_ThrowsConflictWithoutOverwritingNewerValues(bool optOut)
    {
        var analysis = await AddAnalysisAsync();
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstRepository = new AnalysisFollowUpRepository(firstContext);
        var secondRepository = new AnalysisFollowUpRepository(secondContext);
        var first = (await firstRepository.GetPreferencesAsync(analysis.UserId))!;
        var stale = (await secondRepository.GetPreferencesAsync(analysis.UserId))!;
        if (optOut)
            first.SurveysEnabled = false;
        else
            first.NextInvitationAt = DateTimeOffset.UtcNow.AddDays(30);
        await firstRepository.SaveChangesAsync();
        await firstContext.Entry(first).ReloadAsync();

        if (optOut)
            stale.NextInvitationAt = DateTimeOffset.UtcNow.AddDays(14);
        else
            stale.SurveysEnabled = false;

        Func<Task> act = () => secondRepository.SaveChangesAsync();
        await act.Should().ThrowAsync<ConflictException>();

        await using var verification = database.CreateDbContext();
        var saved = await verification.UserFeedbackPreferences.SingleAsync(p => p.UserId == analysis.UserId);
        if (optOut)
            saved.SurveysEnabled.Should().BeFalse();
        else
            saved.NextInvitationAt.Should().Be(first.NextInvitationAt);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAnalysisAlreadyHasAFollowUp_ThrowsConflictWithoutDuplicatingIt()
    {
        var analysis = await AddAnalysisAsync();
        await using (var firstContext = database.CreateDbContext())
        {
            var repository = new AnalysisFollowUpRepository(firstContext);
            repository.Add(new AnalysisFollowUp { AnalysisId = analysis.Id, UserId = analysis.UserId, InvitedAt = DateTimeOffset.UtcNow });
            await repository.SaveChangesAsync();
        }

        await using var secondContext = database.CreateDbContext();
        var secondRepository = new AnalysisFollowUpRepository(secondContext);
        secondRepository.Add(new AnalysisFollowUp { AnalysisId = analysis.Id, UserId = analysis.UserId, InvitedAt = DateTimeOffset.UtcNow });

        Func<Task> act = () => secondRepository.SaveChangesAsync();
        await act.Should().ThrowAsync<ConflictException>();

        await using var verification = database.CreateDbContext();
        (await verification.AnalysisFollowUps.CountAsync(f => f.AnalysisId == analysis.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAForeignKeyIsInvalid_PropagatesTheDatabaseError()
    {
        await using var context = database.CreateDbContext();
        var repository = new AnalysisFollowUpRepository(context);
        repository.Add(new AnalysisFollowUp { AnalysisId = Guid.NewGuid(), UserId = Guid.NewGuid(), InvitedAt = DateTimeOffset.UtcNow });

        Func<Task> act = () => repository.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(exception => exception.InnerException is PostgresException &&
                ((PostgresException)exception.InnerException).SqlState == PostgresErrorCodes.ForeignKeyViolation);
    }

    private async Task<Analysis> AddAnalysisAsync()
    {
        await using var context = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var user = new User { Email = $"follow-up-{Guid.NewGuid():N}@example.invalid", CreatedAt = now, DateOfBirth = new DateOnly(1990, 1, 1) };
        var profile = new Context { UserId = user.Id, Type = ContextType.Analysis, PeopleCount = 1, CreatedAt = now, UpdatedAt = now };
        var analysis = new Analysis { UserId = user.Id, Name = "Follow-up repository test", ContextId = profile.Id, Context = profile, CreatedAt = now.AddDays(-20), UpdatedAt = now };
        context.Users.Add(user);
        context.Analyses.Add(analysis);
        context.UserFeedbackPreferences.Add(new UserFeedbackPreferences { UserId = user.Id, NextInvitationAt = now.AddDays(14) });
        await context.SaveChangesAsync();
        return analysis;
    }
}
