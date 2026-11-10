using FluentAssertions;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Tests.Infrastructure;

public class ZoneProfilesCacheTests
{
    private static readonly IReadOnlyList<ZoneProfile> Profiles = [new ZoneProfile { Slug = "palermo", Name = "Palermo" }];

    [Fact]
    public async Task GetAsync_WhenCalledConcurrently_LoadsOnce()
    {
        var cache = new ZoneProfilesCache(TimeProvider.System);
        var loads = 0;
        async Task<IReadOnlyList<ZoneProfile>> Load(CancellationToken _)
        {
            Interlocked.Increment(ref loads);
            await Task.Delay(50);
            return Profiles;
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => cache.GetAsync(Load, CancellationToken.None)));

        loads.Should().Be(1);
        results.Should().AllSatisfy(result => result.Should().BeSameAs(Profiles));
    }

    [Fact]
    public async Task GetAsync_WhenTheLoadIsEmpty_TriesAgainNextTime()
    {
        var cache = new ZoneProfilesCache(TimeProvider.System);
        await cache.GetAsync(_ => Task.FromResult<IReadOnlyList<ZoneProfile>>([]), CancellationToken.None);

        var result = await cache.GetAsync(_ => Task.FromResult(Profiles), CancellationToken.None);

        result.Should().BeSameAs(Profiles);
    }

    [Fact]
    public async Task GetAsync_WhenInvalidated_LoadsAgain()
    {
        var cache = new ZoneProfilesCache(TimeProvider.System);
        await cache.GetAsync(_ => Task.FromResult(Profiles), CancellationToken.None);
        IReadOnlyList<ZoneProfile> reloaded = [new ZoneProfile { Slug = "belgrano", Name = "Belgrano" }];

        cache.Invalidate();
        var result = await cache.GetAsync(_ => Task.FromResult(reloaded), CancellationToken.None);

        result.Should().BeSameAs(reloaded);
    }
}
