using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedZone;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.SavedZone;

public class ListSavedZonesTests
{
    [Fact]
    public async Task ListSavedZones_ReturnsOnlyTheRepositoryResultsAsResponses()
    {
        var userId = Guid.NewGuid();
        var savedAt = DateTimeOffset.UtcNow;
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DomainSavedZone { UserId = userId, ZoneSlug = "villa-luro", Name = "Home", Subtitle = "Near work", SavedAt = savedAt }
            });

        var result = await new ListSavedZones(repository.Object).ExecuteAsync(userId);

        var savedZone = result.Should().ContainSingle().Which;
        savedZone.Id.Should().Be("villa-luro");
        savedZone.Name.Should().Be("Home");
        savedZone.Subtitle.Should().Be("Near work");
        savedZone.SavedAt.Should().Be(savedAt);
    }

    [Fact]
    public async Task ListSavedZones_WhenUserHasNoSavedZones_ReturnsAnEmptyList()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DomainSavedZone>());

        var result = await new ListSavedZones(repository.Object).ExecuteAsync(userId);

        result.Should().BeEmpty();
    }
}
