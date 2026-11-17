using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class ListAnalysisTrashTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ListAnalysisTrash_WhenUserHasTrashedAnalyses_ReturnsItemsWithTheirExpirationDates()
    {
        var now = AnalysisTrashTestSupport.CurrentTime;
        var groupAnalysis = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Group analysis",
            deletedAt: now.AddDays(-1),
            groupScoresJson: "{}");
        var individualAnalysis = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Individual analysis",
            deletedAt: now.AddDays(-2));
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedForUserAsync(
                _userId,
                now - Analysis.TrashRetentionPeriod,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { groupAnalysis, individualAnalysis });

        var result = await new ListAnalysisTrash(
                repository.Object,
                new AnalysisTrashTestSupport.FixedTimeProvider(now))
            .ExecuteAsync(_userId);

        result.Select(item => item.Id).Should().Equal(groupAnalysis.Id, individualAnalysis.Id);
        result.Should().SatisfyRespectively(
            groupItem =>
            {
                groupItem.Name.Should().Be("Group analysis");
                groupItem.IsGroupAnalysis.Should().BeTrue();
                groupItem.DeletedAt.Should().Be(groupAnalysis.DeletedAt!.Value);
                groupItem.ExpiresAt.Should().Be(groupAnalysis.DeletedAt!.Value.AddDays(30));
            },
            individualItem =>
            {
                individualItem.Name.Should().Be("Individual analysis");
                individualItem.IsGroupAnalysis.Should().BeFalse();
                individualItem.ExpiresAt.Should().Be(individualAnalysis.DeletedAt!.Value.AddDays(30));
            });
        repository.Verify(item => item.GetTrashedForUserAsync(
            _userId,
            now - Analysis.TrashRetentionPeriod,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAnalysisTrash_WhenUserHasNoTrashedAnalyses_ReturnsAnEmptyList()
    {
        var now = AnalysisTrashTestSupport.CurrentTime;
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedForUserAsync(
                _userId,
                now - Analysis.TrashRetentionPeriod,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Analysis>());

        var result = await new ListAnalysisTrash(
                repository.Object,
                new AnalysisTrashTestSupport.FixedTimeProvider(now))
            .ExecuteAsync(_userId);

        result.Should().BeEmpty();
    }
}
