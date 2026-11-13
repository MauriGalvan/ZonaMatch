namespace ZonaMatch.Application.Models;

// Dollar rate (pesos per dollar) the Zonaprop market was loaded with (ZONE-MARKET-DOCKER.bat --usd-ars). The investor
// profile needs it to compare rents in pesos with sale prices in dollars. Configured as Market:UsdArs; update it
// whenever the market is loaded with another rate.
public sealed record MarketExchangeRate(decimal UsdArs)
{
    // Rate of the market load of 2026-11-09
    public const decimal Default = 1568.7m;
}
