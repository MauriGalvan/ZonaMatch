using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class EmptyAnalysisTrashTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task EmptyAnalysisTrash_WhenTrashContainsAnalyses_PermanentlyDeletesAllItems()
    {
        var now = AnalysisTrashTestSupport.CurrentTime;
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original", now);
        var version = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            now,
            original.Id);
        var otherOriginal = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Other", now);
        var deletedAnalyses = new List<Analysis>();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetDeletedForUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { original, version, otherOriginal });
        repository.Setup(item => item.PermanentlyDelete(It.IsAny<Analysis>()))
            .Callback<Analysis>(deletedAnalyses.Add);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await new EmptyAnalysisTrash(repository.Object).ExecuteAsync(_userId);

        deletedAnalyses.Should().Equal(version, original, otherOriginal);
        repository.Verify(item => item.GetDeletedForUserAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmptyAnalysisTrash_WhenTrashIsEmpty_DoesNotSaveChanges()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetDeletedForUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Analysis>());

        await new EmptyAnalysisTrash(repository.Object).ExecuteAsync(_userId);

        repository.Verify(item => item.PermanentlyDelete(It.IsAny<Analysis>()), Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
