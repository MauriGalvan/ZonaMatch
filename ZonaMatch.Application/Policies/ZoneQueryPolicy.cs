using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Policies;

public static class ZoneQueryPolicy
{
    // ~15 m: preserve the shape of a neighborhood while keeping its boundary payload light.
    public const double DefaultBoundaryTolerance = 0.00015;
    public const double DefaultCoverageTolerance = 0.0003;
    public const double SimplifiedCoverageTolerance = 0.001;
    public const double MaximumCoverageTolerance = 0.05;
    public const int MinimumSearchCharacters = 2;
    public const int MaximumSearchResults = 20;

    public static void ValidateSlug(
        string slug,
        string errorMessage = "El identificador de zona tiene que estar en minúsculas, sin acentos y con palabras separadas por guiones (por ejemplo, villa-luro).")
    {
        if (!ZoneSlug.IsValid(slug))
            throw new BadRequestException(errorMessage);
    }

    public static double NormalizeBoundaryTolerance(double? tolerance) =>
        Math.Max(tolerance ?? DefaultBoundaryTolerance, 0);

    public static double NormalizeCoverageTolerance(double? tolerance, bool simplified = false) =>
        Math.Clamp(tolerance ?? (simplified ? SimplifiedCoverageTolerance : DefaultCoverageTolerance), 0d, MaximumCoverageTolerance);
}
