using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public class SubmitAnalysisFollowUpTests
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
    public async Task SubmitAnalysisFollowUp_WhenAnswered_StoresTheResponseOnceAndWaitsFourteenDaysBeforeAnotherInvitation()
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

    [Theory]
    [InlineData("unknown", 3)]
    [InlineData("still-searching", 0)]
    [InlineData("still-searching", 6)]
    [InlineData("still-searching", null)]
    public async Task SubmitAnalysisFollowUp_WhenOutcomeOrHelpfulnessIsInvalid_ThrowsBadRequestWithoutSaving(string outcome, int? helpfulness)
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await Assert.ThrowsAsync<BadRequestException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, _userId, new() { Outcome = outcome, Helpfulness = helpfulness }));
        Assert.Null(Assert.Single(_repository.FollowUps).CompletedAt);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task SubmitAnalysisFollowUp_WhenCommentExceedsMaximumLength_ThrowsBadRequest()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        await Assert.ThrowsAsync<BadRequestException>(() => new SubmitAnalysisFollowUp(_repository, _clock)
            .ExecuteAsync(invitation.Id, _userId, new() { Outcome = "still-searching", Helpfulness = 3, Comment = new string('x', 2001) }));
    }

    [Theory]
    [InlineData("still-searching", 1)]
    [InlineData("visited-recommended-zone", 2)]
    [InlineData("chose-recommended-zone", 3)]
    [InlineData("chose-another-zone", 4)]
    [InlineData("stopped-searching", 5)]
    public async Task SubmitAnalysisFollowUp_WhenOutcomeIsSupported_CompletesTheFollowUpWithoutInvitingForTheSameAnalysisAgain(string outcome, int helpfulness)
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
    public async Task SubmitAnalysisFollowUp_WhenCommentIsOmittedOrBlank_StoresNull(string? comment)
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;

        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "still-searching", Helpfulness = 3, Comment = comment });

        Assert.Null(Assert.Single(_repository.FollowUps).Comment);
        Assert.Equal(2, _repository.SaveCount);
    }

    [Fact]
    public async Task SubmitAnalysisFollowUp_WhenCommentHasMaximumLength_StoresTheComment()
    {
        AddAnalysis(30);
        var invitation = (await Invite.ExecuteAsync(_userId))!;
        var comment = new string('x', 2000);

        await new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(invitation.Id, _userId,
            new() { Outcome = "still-searching", Helpfulness = 3, Comment = comment });

        Assert.Equal(comment, Assert.Single(_repository.FollowUps).Comment);
    }

    [Fact]
    public async Task SubmitAnalysisFollowUp_WhenFollowUpDoesNotExist_ThrowsNotFoundWithoutSaving()
    {
        var id = Guid.NewGuid();
        var userId = _userId;

        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId,
                new() { Outcome = "still-searching", Helpfulness = 3 }));

        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task SubmitAnalysisFollowUp_WhenFollowUpBelongsToAnotherUser_ThrowsNotFoundWithoutSaving()
    {
        AddAnalysis(30);
        var id = (await Invite.ExecuteAsync(_userId))!.Id;
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId,
                new() { Outcome = "still-searching", Helpfulness = 3 }));

        Assert.Null(Assert.Single(_repository.FollowUps).CompletedAt);
        Assert.Null(Assert.Single(_repository.FollowUps).PostponedUntil);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task SubmitAnalysisFollowUp_WhenPreferencesDoNotExist_ThrowsNotFoundWithoutSaving()
    {
        var analysis = AddAnalysis(30);
        var followUp = new FollowUp { UserId = _userId, AnalysisId = analysis.Id, Analysis = analysis, InvitedAt = _clock.Now };
        _repository.Add(followUp);
        var id = followUp.Id;
        var userId = _userId;

        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitAnalysisFollowUp(_repository, _clock).ExecuteAsync(id, userId,
                new() { Outcome = "still-searching", Helpfulness = 3 }));

        Assert.Null(followUp.CompletedAt);
        Assert.Null(followUp.PostponedUntil);
        Assert.Null(followUp.Outcome);
        Assert.Equal(0, _repository.SaveCount);
    }
}
