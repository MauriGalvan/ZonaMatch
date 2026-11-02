using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.SavedZone;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.SavedZone;

public class SaveZoneTests
{
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
    private static readonly AuthenticatedUserContext User = new(Guid.NewGuid(), "ana@example.com");

    [Fact]
    public async Task SaveZone_WhenZoneDoesNotExist_ThrowsNotFoundException()
    {
        var zones = new Mock<IZoneRepository>();
        zones.Setup(item => item.ExistsAsync("unknown", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Func<Task> action = () => new SaveZone(
                new Mock<ISavedZoneRepository>().Object, zones.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync("unknown", User, new SaveZoneRequest { Name = "Home" });

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SaveZone_WhenNameIsBlank_ThrowsBadRequestException()
    {
        var zones = new Mock<IZoneRepository>();
        zones.Setup(item => item.ExistsAsync("villa-luro", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Func<Task> action = () => new SaveZone(
                new Mock<ISavedZoneRepository>().Object, zones.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync("villa-luro", User, new SaveZoneRequest { Name = "   " });

        await action.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task SaveZone_WhenZoneIsNotAlreadySaved_CreatesItWithNormalizedDetails()
    {
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetAsync(User.Id, "villa-luro", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainSavedZone?)null);
        var zones = new Mock<IZoneRepository>();
        zones.Setup(item => item.ExistsAsync("villa-luro", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        DomainSavedZone? addedZone = null;
        repository.Setup(item => item.Add(It.IsAny<DomainSavedZone>()))
            .Callback<DomainSavedZone>(zone => addedZone = zone);

        var response = await new SaveZone(repository.Object, zones.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync("villa-luro", User, new SaveZoneRequest { Name = "  Home  ", Subtitle = "  Near work " });

        addedZone.Should().NotBeNull();
        addedZone!.UserId.Should().Be(User.Id);
        addedZone.ZoneSlug.Should().Be("villa-luro");
        addedZone.SavedAt.Should().Be(CurrentTime);
        response.Name.Should().Be("Home");
        response.Subtitle.Should().Be("Near work");
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveZone_WhenZoneWasAlreadySaved_UpdatesItsDetailsWithoutResettingSavedAt()
    {
        var originalSavedAt = CurrentTime.AddDays(-5);
        var existing = new DomainSavedZone
        {
            UserId = User.Id,
            ZoneSlug = "villa-luro",
            Name = "Old name",
            SavedAt = originalSavedAt
        };
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetAsync(User.Id, "villa-luro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var zones = new Mock<IZoneRepository>();
        zones.Setup(item => item.ExistsAsync("villa-luro", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var response = await new SaveZone(repository.Object, zones.Object, new FixedTimeProvider(CurrentTime))
            .ExecuteAsync("villa-luro", User, new SaveZoneRequest { Name = "New name", Subtitle = "Updated" });

        response.Name.Should().Be("New name");
        existing.SavedAt.Should().Be(originalSavedAt);
        repository.Verify(item => item.Add(It.IsAny<DomainSavedZone>()), Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
