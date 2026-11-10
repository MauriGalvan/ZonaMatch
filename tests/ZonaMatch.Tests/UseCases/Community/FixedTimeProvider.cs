namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => currentTime;
}
