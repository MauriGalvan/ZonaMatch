using ZonaMatch.Application.DTOs.AnalysisFollowUp.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public sealed class AnalysisFollowUpTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeRepository _repository = new();
    private readonly TestClock _clock = new();
    private InviteAnalysisFollowUp Invite => new(_repository, _clock);

    private Analysis AddAnalysis(int ageDays, Guid? originalId = null, Guid? userId = null)
    {
        var analysis = new Analysis
        {
            UserId = userId ?? _userId, Name = "Near work", OriginalAnalysisId = originalId,
            CreatedAt = _clock.Now.AddDays(-ageDays), UpdatedAt = _clock.Now,
        };
        _repository.Analyses.Add(analysis);
        return analysis;
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(30, true)]
    public async Task Invite_UsesTheOriginalCreationDateAndFourteenDayBoundary(int ageDays, bool eligible)
    {
        AddAnalysis(ageDays);
        var result = await Invite.ExecuteAsync(_userId);
        Assert.Equal(eligible, result is not null);
        Assert.Equal(eligible ? 1 : 0, _repository.SaveCount);
    }

    [Fact]
    public async Task Invite_IgnoresOtherUsersAndVersions()
    {
        var original = AddAnalysis(13);
        AddAnalysis(30, original.Id);
        AddAnalysis(40, userId: Guid.NewGuid());
        Assert.Null(await Invite.ExecuteAsync(_userId));
    }

    [Fact]
    public async Task Invite_ReturnsTheSameCardAcrossSessionsWithoutCreatingAnotherInvitation()
    {
        var original = AddAnalysis(30);
        AddAnalysis(20);
        var first = await Invite.ExecuteAsync(_userId);
        var second = await Invite.ExecuteAsync(_userId);
        Assert.Equal(original.Id, first!.AnalysisId);
        Assert.Equal(first.Id, second!.Id);
        Assert.Single(_repository.FollowUps);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Submit_StoresTheAnswerOnceAndWaitsFourteenDaysBeforeTheNextAnalysis()
    {
        AddAnalysis(30);
        var next = AddAnalysis(20);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var submit = new SubmitAnalysisFollowUp(_repository, _clock);
        await submit.ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "chose-recommended-zone", Helpfulness = 5, Comment = "  Very useful  " });
        var answer = Assert.Single(_repository.FollowUps);
        Assert.Equal("Very useful", answer.Comment);
        Assert.Equal(_clock.Now, answer.CompletedAt);
        await Assert.ThrowsAsync<ConflictException>(() => submit.ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "still-searching", Helpfulness = 2 }));

        _clock.Now = _clock.Now.AddDays(14).AddTicks(-1);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        _clock.Now = _clock.Now.AddTicks(1);
        Assert.Equal(next.Id, (await Invite.ExecuteAsync(_userId))!.AnalysisId);
    }

    [Fact]
    public async Task Postpone_HidesAllInvitationsForThirtyDaysAndThenReusesTheSameFollowUp()
    {
        AddAnalysis(30);
        AddAnalysis(20);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await new PostponeAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId);
        _clock.Now = _clock.Now.AddDays(30).AddTicks(-1);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        _clock.Now = _clock.Now.AddTicks(1);
        Assert.Equal(invitation.Id, (await Invite.ExecuteAsync(_userId))!.Id);
        Assert.Single(_repository.FollowUps);
    }

    [Fact]
    public async Task Preferences_DisablingSurveysHidesAnActiveInvitationAndCanBeReenabled()
    {
        AddAnalysis(30);
        Assert.True((await new GetFeedbackPreferences(_repository).ExecuteAsync(_userId)).SurveysEnabled);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var update = new UpdateFeedbackPreferences(_repository);
        await update.ExecuteAsync(_userId, false);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        Assert.False((await new GetFeedbackPreferences(_repository).ExecuteAsync(_userId)).SurveysEnabled);
        await update.ExecuteAsync(_userId, true);
        Assert.Equal(invitation.Id, (await Invite.ExecuteAsync(_userId))!.Id);
    }

    [Theory]
    [InlineData("unknown", 3)]
    [InlineData("still-searching", 0)]
    [InlineData("still-searching", 6)]
    [InlineData("still-searching", null)]
    public async Task Submit_RejectsInvalidOutcomesAndHelpfulness(string outcome, int? helpfulness)
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await Assert.ThrowsAsync<BadRequestException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, _userId, new() { Outcome = outcome, Helpfulness = helpfulness }));
        Assert.Null(Assert.Single(_repository.FollowUps).CompletedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Submit_RejectsAnOversizedComment()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await Assert.ThrowsAsync<BadRequestException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, _userId, new() { Outcome = "still-searching", Helpfulness = 3, Comment = new string('x', 2001) }));
    }

    [Fact]
    public async Task FollowUp_AnotherUserCannotAnswerOrPostponeIt()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var otherUser = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, otherUser, new() { Outcome = "still-searching", Helpfulness = 3 }));
        await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, otherUser));
    }

    [Fact]
    public async Task Preferences_CanOptOutBeforeTheFirstInvitationWithoutCreatingAFollowUp()
    {
        AddAnalysis(30);
        var result = await new UpdateFeedbackPreferences(_repository).ExecuteAsync(_userId, false);

        Assert.False(result.SurveysEnabled);
        Assert.False((await new GetFeedbackPreferences(_repository).ExecuteAsync(_userId)).SurveysEnabled);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        Assert.Empty(_repository.FollowUps);
        Assert.Equal(1, _repository.SaveCount);

        await new UpdateFeedbackPreferences(_repository).ExecuteAsync(_userId, true);
        Assert.NotNull(await Invite.ExecuteAsync(_userId));
    }

    [Fact]
    public async Task Invite_ReturnsTheCurrentAnalysisNameForAnExistingInvitation()
    {
        var analysis = AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        analysis.Name = "Updated search";

        var result = (await Invite.ExecuteAsync(_userId))!;

        Assert.Equal(invitation.Id, result.Id);
        Assert.Equal("Updated search", result.AnalysisName);
        Assert.Equal(analysis.CreatedAt, result.AnalysisCreatedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Theory]
    [InlineData("still-searching", 1)]
    [InlineData("visited-recommended-zone", 2)]
    [InlineData("chose-recommended-zone", 3)]
    [InlineData("chose-another-zone", 4)]
    [InlineData("stopped-searching", 5)]
    public async Task Submit_AcceptsEverySupportedOutcomeAndNeverInvitesAgainForTheAnsweredAnalysis(string outcome, int helpfulness)
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;

        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = outcome, Helpfulness = helpfulness });

        var saved = Assert.Single(_repository.FollowUps);
        Assert.Equal(outcome, saved.Outcome);
        Assert.Equal(helpfulness, saved.Helpfulness);
        Assert.Equal(_clock.Now, saved.CompletedAt);
        Assert.Null(saved.PostponedUntil);
        _clock.Now = _clock.Now.AddDays(180);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        Assert.Equal(2, _repository.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public async Task Submit_StoresAnOmittedOrBlankCommentAsNull(string? comment)
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;

        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "still-searching", Helpfulness = 3, Comment = comment });

        Assert.Null(Assert.Single(_repository.FollowUps).Comment);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task Submit_AcceptsACommentAtTheMaximumLength()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var comment = new string('x', 2000);

        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "still-searching", Helpfulness = 3, Comment = comment });

        Assert.Equal(comment, Assert.Single(_repository.FollowUps).Comment);
    }

    [Fact]
    public async Task Postpone_RejectsAnAlreadyAnsweredFollowUpWithoutChangingTheAnswerOrCooldown()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "stopped-searching", Helpfulness = 4, Comment = "Finished" });
        var preferences = (await _repository.GetPreferencesAsync(_userId))!;
        var nextInvitationAt = preferences.NextInvitationAt;

        await Assert.ThrowsAsync<ConflictException>(() => new PostponeAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, _userId));

        var saved = Assert.Single(_repository.FollowUps);
        Assert.Equal("stopped-searching", saved.Outcome);
        Assert.Equal("Finished", saved.Comment);
        Assert.Null(saved.PostponedUntil);
        Assert.Equal(nextInvitationAt, preferences.NextInvitationAt);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task Postpone_CanWaitAnotherThirtyDaysAfterTheInvitationReappears()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var postpone = new PostponeAnalysisFollowUp(_repository, _clock);
        await postpone.ExecuteAsync(invitation.Id, _userId);
        _clock.Now = _clock.Now.AddDays(30);
        Assert.Equal(invitation.Id, (await Invite.ExecuteAsync(_userId))!.Id);
        await postpone.ExecuteAsync(invitation.Id, _userId);

        _clock.Now = _clock.Now.AddDays(30).AddTicks(-1);
        Assert.Null(await Invite.ExecuteAsync(_userId));
        _clock.Now = _clock.Now.AddTicks(1);
        Assert.Equal(invitation.Id, (await Invite.ExecuteAsync(_userId))!.Id);
        Assert.Null(Assert.Single(_repository.FollowUps).CompletedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FollowUp_RejectsAMissingInvitationWithoutSaving(bool submit)
    {
        var id = Guid.NewGuid();
        if (submit)
            await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
                .ExecuteAsync(id, _userId, new() { Outcome = "still-searching", Helpfulness = 3 }));
        else
            await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock)
                .ExecuteAsync(id, _userId));
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FollowUp_RejectsMissingPreferencesWithoutChangingTheInvitation(bool submit)
    {
        // An inconsistent store must not silently recreate preferences while handling an answer.
        var analysis = AddAnalysis(30);
        var followUp = new FollowUp { UserId = _userId, AnalysisId = analysis.Id, Analysis = analysis, InvitedAt = _clock.Now };
        _repository.Add(followUp);

        if (submit)
            await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
                .ExecuteAsync(followUp.Id, _userId, new() { Outcome = "still-searching", Helpfulness = 3 }));
        else
            await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock)
                .ExecuteAsync(followUp.Id, _userId));

        Assert.Null(followUp.CompletedAt);
        Assert.Null(followUp.PostponedUntil);
        Assert.Null(followUp.Outcome);
        Assert.Equal(0, _repository.SaveCount);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeRepository : IAnalysisFollowUpRepository
    {
        public List<Analysis> Analyses { get; } = [];
        public List<FollowUp> FollowUps { get; } = [];
        private readonly List<UserFeedbackPreferences> _preferences = [];
        public int SaveCount { get; private set; }
        public Task<UserFeedbackPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_preferences.SingleOrDefault(p => p.UserId == userId));
        public Task<FollowUp?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(FollowUps.FirstOrDefault(f => f.UserId == userId && f.CompletedAt is null && f.PostponedUntil is null));
        public Task<FollowUp?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(FollowUps.SingleOrDefault(f => f.Id == id && f.UserId == userId));
        public Task<FollowUp?> GetForAnalysisAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(FollowUps.SingleOrDefault(f => f.AnalysisId == analysisId && f.UserId == userId));
        public Task<Analysis?> GetEligibleAnalysisAsync(Guid userId, DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(Analyses.Where(a => a.UserId == userId && a.OriginalAnalysisId is null && a.CreatedAt <= createdBefore)
                .Where(a => !FollowUps.Any(f => f.AnalysisId == a.Id && (f.CompletedAt is not null || f.PostponedUntil > now)))
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).FirstOrDefault());
        public void Add(FollowUp followUp) => FollowUps.Add(followUp);
        public void Add(UserFeedbackPreferences preferences) => _preferences.Add(preferences);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
    }
}
