using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public class UpdateFeedbackPreferencesTests
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
    public async Task UpdateFeedbackPreferences_WhenSurveysAreDisabledAndReenabled_HidesAndRestoresTheActiveInvitation()
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

    [Fact]
    public async Task UpdateFeedbackPreferences_WhenOptingOutBeforeTheFirstInvitation_CreatesPreferencesWithoutInviting()
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
}
