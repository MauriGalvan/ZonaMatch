using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class PermanentlyDeleteAnalysisTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task PermanentlyDeleteAnalysis_WhenOriginalIsInTrash_DeletesOriginalAndVersions()
    {
        var now = AnalysisTrashTestSupport.CurrentTime;
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original", now);
        var firstVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            now,
            original.Id);
        var secondVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 3",
            now,
            original.Id);
        var deletedAnalyses = new List<Analysis>();
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(original.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.ListVersionsIncludingDeletedAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstVersion, secondVersion });
        repository.Setup(item => item.PermanentlyDelete(It.IsAny<Analysis>()))
            .Callback<Analysis>(deletedAnalyses.Add);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await new PermanentlyDeleteAnalysis(repository.Object).ExecuteAsync(original.Id, _userId);

        deletedAnalyses.Should().Equal(firstVersion, secondVersion, original);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PermanentlyDeleteAnalysis_WhenVersionIsInTrash_DeletesOnlyThatVersion()
    {
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original");
        var version = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            AnalysisTrashTestSupport.CurrentTime,
            original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(version.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await new PermanentlyDeleteAnalysis(repository.Object).ExecuteAsync(version.Id, _userId);

        repository.Verify(item => item.PermanentlyDelete(version), Times.Once);
        repository.Verify(item => item.PermanentlyDelete(original), Times.Never);
        repository.Verify(
            item => item.ListVersionsIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PermanentlyDeleteAnalysis_WhenAnalysisIsNotInTrash_ThrowsNotFoundWithoutDeleting()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(It.IsAny<Guid>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Analysis?)null);

        Func<Task> action = () => new PermanentlyDeleteAnalysis(repository.Object).ExecuteAsync(Guid.NewGuid(), _userId);

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.PermanentlyDelete(It.IsAny<Analysis>()), Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
