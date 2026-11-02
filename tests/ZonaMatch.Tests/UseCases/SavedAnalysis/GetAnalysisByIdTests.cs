using FluentAssertions;
using Moq;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

public class GetAnalysisByIdTests
{
    [Fact]
    public async Task GetAnalysisById_WhenOriginalIsRequested_ReturnsItsVersions()
    {
        var userId = Guid.NewGuid();
        var original = new Analysis { UserId = userId, Name = "Original", Context = new Context() };
        var version = new Analysis
        {
            UserId = userId,
            Name = "Version 2",
            OriginalAnalysisId = original.Id,
            Context = new Context()
        };
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetAsync(original.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);
        repository.Setup(item => item.ListVersionsAsync(original.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { version });

        var result = await new GetAnalysisById(repository.Object).ExecuteAsync(original.Id, userId);

        result.Name.Should().Be("Original");
        result.Versions.Should().ContainSingle().Which.Name.Should().Be("Version 2");
    }

    [Fact]
    public async Task GetAnalysisById_WhenAnalysisIsNotOwnedByUser_ThrowsNotFound()
    {
        var repository = new Mock<IAnalysisRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Analysis?)null);

        Func<Task> action = () => new GetAnalysisById(repository.Object).ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }
}
