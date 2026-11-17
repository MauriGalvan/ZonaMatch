namespace ZonaMatch.Infrastructure.Geo
{
    // Language is optional: without it Photon returns the default name tag, which in the AMBA is already Spanish.
    public sealed record PhotonSearchOptions(string? Language = null);
}
