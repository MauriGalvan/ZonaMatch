using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public class PostponeAnalysisFollowUpTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeAnalysisFollowUpRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
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

    [Fact]
    public async Task PostponeAnalysisFollowUp_WhenPostponed_HidesInvitationsForThirtyDaysThenReusesTheFollowUp()
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
    public async Task PostponeAnalysisFollowUp_WhenAlreadyAnswered_ThrowsConflictWithoutChangingTheResponseOrWaitingPeriod()
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
    public async Task PostponeAnalysisFollowUp_WhenPostponedAgain_WaitsAnotherThirtyDays()
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

    [Fact]
    public async Task PostponeAnalysisFollowUp_WhenFollowUpDoesNotExist_ThrowsNotFoundWithoutSaving()
    {
        var id = Guid.NewGuid();
        var userId = _userId;

        await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId));

        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task PostponeAnalysisFollowUp_WhenFollowUpBelongsToAnotherUser_ThrowsNotFoundWithoutSaving()
    {
        AddAnalysis(30);
        var id = (await Invite.ExecuteAsync(_userId))!.Id;
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId));

        Assert.Null(Assert.Single(_repository.FollowUps).CompletedAt);
        Assert.Null(Assert.Single(_repository.FollowUps).PostponedUntil);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task PostponeAnalysisFollowUp_WhenPreferencesDoNotExist_ThrowsNotFoundWithoutSaving()
    {
        var analysis = AddAnalysis(30);
        var followUp = new FollowUp { UserId = _userId, AnalysisId = analysis.Id, Analysis = analysis, InvitedAt = _clock.Now };
        _repository.Add(followUp);
        var id = followUp.Id;
        var userId = _userId;

        await Assert.ThrowsAsync<NotFoundException>(() => new PostponeAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId));

        Assert.Null(followUp.CompletedAt);
        Assert.Null(followUp.PostponedUntil);
        Assert.Null(followUp.Outcome);
        Assert.Equal(0, _repository.SaveCount);
    }
}
