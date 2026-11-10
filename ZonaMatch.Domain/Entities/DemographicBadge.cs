namespace ZonaMatch.Domain.Entities;

// Evaluated badge for a demographic comparison against a reference city.
public sealed record DemographicBadge(string Label, string Variant)
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Success = "success";
}
