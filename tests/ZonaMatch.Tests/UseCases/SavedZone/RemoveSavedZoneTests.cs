using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.SavedZone;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.SavedZone;

public class RemoveSavedZoneTests
{
    [Fact]
    public async Task RemoveSavedZone_WhenZoneIsNotSaved_IsIdempotent()
    {
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainSavedZone?)null);

        await new RemoveSavedZone(repository.Object).ExecuteAsync("villa-luro", Guid.NewGuid());

        repository.Verify(item => item.Remove(It.IsAny<DomainSavedZone>()), Times.Never);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveSavedZone_WhenZoneIsSaved_RemovesItAndPersists()
    {
        var userId = Guid.NewGuid();
        var savedZone = new DomainSavedZone { UserId = userId, ZoneSlug = "villa-luro", Name = "Home" };
        var repository = new Mock<ISavedZoneRepository>();
        repository.Setup(item => item.GetAsync(userId, "villa-luro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedZone);

        await new RemoveSavedZone(repository.Object).ExecuteAsync("villa-luro", userId);

        repository.Verify(item => item.Remove(savedZone), Times.Once);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
