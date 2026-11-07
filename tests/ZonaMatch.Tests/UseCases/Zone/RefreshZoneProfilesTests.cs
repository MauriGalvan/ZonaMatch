using FluentAssertions;
using Moq;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Zone;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Zone
{
    public class RefreshZoneProfilesTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenCalled_DropsTheCachedProfilesBeforeLoadingThemAgain()
        {
            var calls = new List<string>();
            var repository = new Mock<IZoneProfilesRepository>();
            repository.Setup(r => r.InvalidateAsync(It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("invalidate"))
                .Returns(Task.CompletedTask);
            repository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .Callback(() => calls.Add("list"))
                .ReturnsAsync([new ZoneProfile { Slug = "palermo" }, new ZoneProfile { Slug = "recoleta" }]);

            var response = await new RefreshZoneProfiles(repository.Object).ExecuteAsync();

            calls.Should().Equal("invalidate", "list");
            response.Zones.Should().Be(2);
        }
    }
}
