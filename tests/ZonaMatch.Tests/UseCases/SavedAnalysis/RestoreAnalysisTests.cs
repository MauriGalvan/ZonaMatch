using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public sealed class RestoreAnalysisTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = AnalysisTrashTestSupport.CurrentTime;

    [Fact]
    public async Task RestoreAnalysis_WhenOriginalIsInTrash_RestoresOriginalAndAllVersions()
    {
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original", _now.AddDays(-1));
        var firstVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            _now.AddDays(-1),
            original.Id);
        var secondVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 3",
            _now.AddDays(-1),
            original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(original.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.ListVersionsIncludingDeletedAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstVersion, secondVersion });
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateUseCase(repository).ExecuteAsync(original.Id, _userId);

        original.DeletedAt.Should().BeNull();
        firstVersion.DeletedAt.Should().BeNull();
        secondVersion.DeletedAt.Should().BeNull();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreAnalysis_WhenVersionIsInTrashAndOriginalIsActive_RestoresOnlyThatVersion()
    {
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original");
        var version = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            _now.AddDays(-1),
            original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(version.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        repository.Setup(item => item.GetIncludingDeletedAsync(original.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateUseCase(repository).ExecuteAsync(version.Id, _userId);

        version.DeletedAt.Should().BeNull();
        original.DeletedAt.Should().BeNull();
        repository.Verify(
            item => item.ListVersionsIncludingDeletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreAnalysis_WhenVersionAndOriginalAreInTrash_RestoresTheWholeAnalysis()
    {
        var deletedAt = _now.AddDays(-1);
        var original = AnalysisTrashTestSupport.CreateAnalysis(_userId, "Original", deletedAt);
        var selectedVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 2",
            deletedAt,
            original.Id);
        var otherVersion = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Version 3",
            deletedAt,
            original.Id);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(selectedVersion.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedVersion);
        repository.Setup(item => item.GetIncludingDeletedAsync(original.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.ListVersionsIncludingDeletedAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { selectedVersion, otherVersion });
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateUseCase(repository).ExecuteAsync(selectedVersion.Id, _userId);

        original.DeletedAt.Should().BeNull();
        selectedVersion.DeletedAt.Should().BeNull();
        otherVersion.DeletedAt.Should().BeNull();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreAnalysis_WhenRetentionPeriodHasExpired_ThrowsNotFoundWithoutSaving()
    {
        var expiredAnalysis = AnalysisTrashTestSupport.CreateAnalysis(
            _userId,
            "Expired",
            _now - Analysis.TrashRetentionPeriod);
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(expiredAnalysis.Id, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredAnalysis);

        Func<Task> action = () => CreateUseCase(repository).ExecuteAsync(expiredAnalysis.Id, _userId);

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestoreAnalysis_WhenAnalysisDoesNotBelongToTheUser_ThrowsNotFoundWithoutSaving()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetTrashedAsync(It.IsAny<Guid>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Analysis?)null);

        Func<Task> action = () => CreateUseCase(repository).ExecuteAsync(Guid.NewGuid(), _userId);

        await action.Should().ThrowAsync<NotFoundException>();
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private RestoreAnalysis CreateUseCase(Mock<IAnalysisRepository> repository) =>
        new(repository.Object, new AnalysisTrashTestSupport.FixedTimeProvider(_now));
}
