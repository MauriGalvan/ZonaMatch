using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

public class GetFeedbackPreferencesTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeAnalysisFollowUpRepository _repository = new();

    [Fact]
    public async Task GetFeedbackPreferences_WhenPreferencesDoNotExist_ReturnsSurveysEnabledWithoutSaving()
    {
        var result = await new GetFeedbackPreferences(_repository).ExecuteAsync(_userId);

        Assert.True(result.SurveysEnabled);
        Assert.Null(await _repository.GetPreferencesAsync(_userId));
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetFeedbackPreferences_WhenPreferencesExist_ReturnsTheStoredValueWithoutSaving(bool surveysEnabled)
    {
        _repository.Add(new UserFeedbackPreferences { UserId = _userId, SurveysEnabled = surveysEnabled });

        var result = await new GetFeedbackPreferences(_repository).ExecuteAsync(_userId);

        Assert.Equal(surveysEnabled, result.SurveysEnabled);
        Assert.Equal(0, _repository.SaveCount);
    }
}
