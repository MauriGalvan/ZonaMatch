using ZonaMatch.Application.Exceptions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Policies;

public static class ZoneQueryPolicy
{
    // ~15 m: preserve the shape of a neighborhood while keeping its boundary payload light.
    public const double DefaultBoundaryTolerance = 0.00015;
    public const int MinimumSearchCharacters = 2;
    public const int MaximumSearchResults = 20;

    public static void ValidateSlug(
        string slug,
        string errorMessage = "El identificador de zona debe tener minusculas sin acentos separadas por guiones (p. ej. villa-luro).")
    {
        if (!SlugZona.EsValido(slug))
            throw new BadRequestException(errorMessage);
    }

    public static double NormalizeBoundaryTolerance(double? tolerance) =>
        Math.Max(tolerance ?? DefaultBoundaryTolerance, 0);
}
