using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public class InviteAnalysisFollowUpTests
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

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(30, true)]
    public async Task InviteAnalysisFollowUp_WhenAnalysisAgeIsChecked_InvitesOnlyOriginalsAtLeastFourteenDaysOld(int ageDays, bool eligible)
    {
        AddAnalysis(ageDays);
        var result = await Invite.ExecuteAsync(_userId);
        Assert.Equal(eligible, result is not null);
        Assert.Equal(eligible ? 1 : 0, _repository.SaveCount);
    }

    [Fact]
    public async Task InviteAnalysisFollowUp_WhenOnlyOtherUsersAnalysesOrVersionsAreOldEnough_ReturnsNull()
    {
        var original = AddAnalysis(13);
        AddAnalysis(30, original.Id);
        AddAnalysis(40, userId: Guid.NewGuid());
        Assert.Null(await Invite.ExecuteAsync(_userId));
    }

    [Fact]
    public async Task InviteAnalysisFollowUp_WhenAnActiveInvitationExists_ReturnsItWithoutCreatingAnother()
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
    public async Task InviteAnalysisFollowUp_WhenTheAnalysisIsRenamed_ReturnsItsCurrentName()
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
}
