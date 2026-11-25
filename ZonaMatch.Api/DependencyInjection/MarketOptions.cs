namespace ZonaMatch.Api.DependencyInjection;

internal sealed class MarketOptions
{
    public const string SectionName = "Market";

    // A malformed value must leave only the investment ranking unavailable.
    public string? UsdArs { get; set; }
}
