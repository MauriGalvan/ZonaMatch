using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetZoneCoverageTests
{
    private const string ExpectedGeoJson = """{"type":"FeatureCollection","features":[]}""";

    [Fact]
    public async Task ExecuteAsync_WhenCalledWithDefaults_QueriesRepositoryWithDefaultTolerance()
    {
        // Arrange
        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.GetCoverageGeoJsonAsync(ZoneQueryPolicy.DefaultCoverageTolerance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedGeoJson);

        var useCase = new GetZoneCoverage(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync();

        // Assert
        result.Should().Be(ExpectedGeoJson);
        repository.Verify(
            r => r.GetCoverageGeoJsonAsync(ZoneQueryPolicy.DefaultCoverageTolerance, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSimplifiedIsTrue_QueriesRepositoryWithSimplifiedTolerance()
    {
        // Arrange
        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.GetCoverageGeoJsonAsync(ZoneQueryPolicy.SimplifiedCoverageTolerance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedGeoJson);

        var useCase = new GetZoneCoverage(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync(simplified: true);

        // Assert
        result.Should().Be(ExpectedGeoJson);
        repository.Verify(
            r => r.GetCoverageGeoJsonAsync(ZoneQueryPolicy.SimplifiedCoverageTolerance, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0.0005, 0.0005)]
    [InlineData(-0.01, 0.0)]
    [InlineData(0.1, 0.05)]
    public async Task ExecuteAsync_WhenToleranceIsProvided_ClampsAndPassesNormalizedTolerance(
        double inputTolerance, double expectedTolerance)
    {
        // Arrange
        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.GetCoverageGeoJsonAsync(expectedTolerance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpectedGeoJson);

        var useCase = new GetZoneCoverage(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync(simplified: false, tolerance: inputTolerance);

        // Assert
        result.Should().Be(ExpectedGeoJson);
        repository.Verify(
            r => r.GetCoverageGeoJsonAsync(expectedTolerance, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsProvided_PropagatesToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.GetCoverageGeoJsonAsync(It.IsAny<double>(), token))
            .ReturnsAsync(ExpectedGeoJson);

        var useCase = new GetZoneCoverage(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync(cancellationToken: token);

        // Assert
        result.Should().Be(ExpectedGeoJson);
        repository.Verify(
            r => r.GetCoverageGeoJsonAsync(It.IsAny<double>(), token),
            Times.Once);
    }
}
