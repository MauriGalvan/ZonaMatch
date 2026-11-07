using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Infrastructure.Repositories;

namespace ZonaMatch.Tests.Infrastructure;

// The zone profiles cached as one entry; HybridCache serializes them to measure their size.
public class ZoneProfileSetTests
{
    private static ZoneProfileSet Set() => new(
    [
        new ZoneProfile
        {
            Slug = "palermo", Name = "Palermo", Level = 10,
            MarketByRooms = new Dictionary<(string Type, int Rooms), HousingMarket>
            {
                [(HousingTypes.Apartment, 2)] = new(1_229_000, 1_353, 250_000, 170_547, 3_560),
                [(HousingTypes.All, 1)] = new(872_000, 1_029, null, 120_733, 2_785),
            },
        },
    ]);

    [Fact]
    public async Task GetOrCreateAsync_WhenProfilesHavePricesByRooms_CachesTheSameInstance()
    {
        var cache = HybridCacheTestSupport.Create();
        var options = new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableDistributedCache };
        var set = Set();

        var first = await cache.GetOrCreateAsync("zone-profiles", _ => ValueTask.FromResult(set), options);
        var second = await cache.GetOrCreateAsync("zone-profiles", _ => ValueTask.FromResult(Set()), options);

        first.Should().BeSameAs(set);
        second.Should().BeSameAs(set);
    }

    [Fact]
    public void Deserialize_WhenSerialized_KeepsThePricesByRooms()
    {
        var json = JsonSerializer.Serialize(Set());

        var copy = JsonSerializer.Deserialize<ZoneProfileSet>(json)!;

        copy.Profiles.Should().ContainSingle();
        copy.Profiles[0].MarketByRooms.Keys.Should().BeEquivalentTo([(HousingTypes.Apartment, 2), (HousingTypes.All, 1)]);
        copy.Profiles[0].GetMarket(HousingTypes.Apartment, 2).AverageRent.Should().Be(1_229_000);
    }
}
