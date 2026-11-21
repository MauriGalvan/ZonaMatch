using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Tests.UseCases.Zone;

public class GetAvailableZonesTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsAvailableZonesFromRepository()
    {
        // Arrange
        var expectedZones = new List<AvailableZoneResponse>
        {
            new("palermo", "Palermo", "barrio", true),
            new("comuna-1", "Comuna 1", "comuna", true),
            new("vicente-lopez", "Vicente López", "partido", true)
        };

        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.ListAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedZones);

        var useCase = new GetAvailableZones(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedZones);
        repository.Verify(r => r.ListAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsProvided_PropagatesToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var repository = new Mock<IZoneRepository>();
        repository
            .Setup(r => r.ListAvailableAsync(token))
            .ReturnsAsync(new List<AvailableZoneResponse>());

        var useCase = new GetAvailableZones(repository.Object);

        // Act
        var result = await useCase.ExecuteAsync(token);

        // Assert
        result.Should().BeEmpty();
        repository.Verify(r => r.ListAvailableAsync(token), Times.Once);
    }
}
