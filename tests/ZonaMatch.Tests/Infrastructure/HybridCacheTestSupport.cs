using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ZonaMatch.Tests.Infrastructure;

// A real in-memory HybridCache, as registered by the API (AddHybridCache), for the infrastructure tests.
internal static class HybridCacheTestSupport
{
    public static HybridCache Create() =>
        new ServiceCollection()
            .AddHybridCache().Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();
}
