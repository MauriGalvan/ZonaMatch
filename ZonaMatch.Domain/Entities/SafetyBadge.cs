namespace ZonaMatch.Domain.Entities;

// Evaluated badge for a safety comparison against a reference city.
public sealed record SafetyBadge(string Label, string Variant)
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Success = "success";
}
