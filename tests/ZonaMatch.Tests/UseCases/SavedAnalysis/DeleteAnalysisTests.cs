using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class DeleteAnalysisTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = AnalysisTrashTestSupport.CurrentTime;

    [Fact]
    public async Task DeleteAnalysis_WhenOriginalIsDeleted_MovesItAndAllVersionsToTrash()
    {
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original");
        var firstVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            _now.AddDays(-2),
            original.Id);
        var secondVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 3",
            originalAnalysisId: original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetAsync(original.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.ListVersionsIncludingDeletedAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstVersion, secondVersion });
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateUseCase(repository).ExecuteAsync(original.Id, _userId);

        original.DeletedAt.Should().Be(_now);
        firstVersion.DeletedAt.Should().Be(_now);
        secondVersion.DeletedAt.Should().Be(_now);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAnalysis_WhenVersionIsDeleted_MovesOnlyThatVersionToTrash()
    {
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original");
        var selectedVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            originalAnalysisId: original.Id);
        var otherVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 3",
            originalAnalysisId: original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetAsync(selectedVersion.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedVersion);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateUseCase(repository).ExecuteAsync(selectedVersion.Id, _userId);

        selectedVersion.DeletedAt.Should().Be(_now);
        original.DeletedAt.Should().BeNull();
        otherVersion.DeletedAt.Should().BeNull();
        repository.Verify(
            item => item.ListVersionsIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAnalysis_WhenAnalysisDoesNotExist_ThrowsNotFoundWithoutSaving()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<Guid>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Analysis?)null);

        Func<Task> action = () => CreateUseCase(repository).ExecuteAsync(Guid.NewGuid(), _userId);

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private DeleteAnalysis CreateUseCase(Mock<IAnalysisRepository> repository) =>
        new(repository.Object, new AnalysisTrashTestSupport.FixedTimeProvider(_now));
}
