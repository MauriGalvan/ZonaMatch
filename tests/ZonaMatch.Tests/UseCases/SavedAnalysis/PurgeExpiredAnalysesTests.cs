using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class PurgeExpiredAnalysesTests
{
    private readonly DateTimeOffset _now = AnalysisTrashTestSupport.CurrentTime;

    [Fact]
    public async Task PurgeExpiredAnalyses_WhenExpiredItemsExist_DeletesThemAndReturnsTheirCount()
    {
        var userId = Guid.NewGuid();
        var original = AnalysisTrashTestSupport.CreateAnalysis(userId, "Original", _now.AddDays(-31));
        var version = AnalysisTrashTestSupport.CreateAnalysis(
            userId,
            "Version 2",
            _now.AddDays(-31),
            original.Id);
        var expiredStandaloneAnalysis = AnalysisTrashTestSupport.CreateAnalysis(
            userId,
            "Standalone",
            _now.AddDays(-30).AddTicks(-1));
        var deletedAnalyses = new List<Analysis>();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetExpiredTrashedAsync(
                _now - Analysis.TrashRetentionPeriod,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { original, version, expiredStandaloneAnalysis });
        repository.Setup(item => item.PermanentlyDelete(It.IsAny<Analysis>()))
            .Callback<Analysis>(deletedAnalyses.Add);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var purgedCount = await new PurgeExpiredAnalyses(
                repository.Object,
                new AnalysisTrashTestSupport.FixedTimeProvider(_now))
            .ExecuteAsync();

        purgedCount.Should().Be(3);
        deletedAnalyses.Should().Equal(version, original, expiredStandaloneAnalysis);
        repository.Verify(item => item.GetExpiredTrashedAsync(
            _now - Analysis.TrashRetentionPeriod,
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeExpiredAnalyses_WhenNoItemsHaveExpired_ReturnsZeroWithoutSaving()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetExpiredTrashedAsync(
                _now - Analysis.TrashRetentionPeriod,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Analysis>());

        var purgedCount = await new PurgeExpiredAnalyses(
                repository.Object,
                new AnalysisTrashTestSupport.FixedTimeProvider(_now))
            .ExecuteAsync();

        purgedCount.Should().Be(0);
        repository.Verify(item => item.PermanentlyDelete(It.IsAny<Analysis>()), Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
