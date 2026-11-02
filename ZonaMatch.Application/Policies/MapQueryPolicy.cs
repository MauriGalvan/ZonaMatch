using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;

namespace ZonaMatch.Application.Policies;

public static class MapQueryPolicy
{
    // Upper bound of features per viewport request, to keep payloads and map rendering reasonable.
    public const int MaximumFeaturesPerQuery = 5_000;

    // ~55 m: invisible at city/province zoom, while greatly reducing polygon vertices.
    private const double DefaultTolerance = 0.0005;
    private const double MaximumTolerance = 0.05;
    private const double AssumedMapWidthPixels = 1500;

    public static void ValidateViewport(BoundingBox boundingBox)
    {
        if (!boundingBox.EsValido)
            throw new BadRequestException(
                "El viewport es invalido: indica minLon, minLat, maxLon y maxLat en grados, con min menor que max.");
    }

    public static void ValidateFeatureCount(int count, string layer)
    {
        if (count > MaximumFeaturesPerQuery)
            throw new BadRequestException(
                $"El viewport contiene mas de {MaximumFeaturesPerQuery} {layer}. Acerca el mapa o aplica filtros.");
    }

    public static double NormalizeTolerance(double? tolerance, double? defaultTolerance = null) =>
        Math.Clamp(tolerance ?? defaultTolerance ?? DefaultTolerance, 0d, MaximumTolerance);

    public static double GetViewportTolerance(BoundingBox boundingBox) =>
        (boundingBox.MaxLon - boundingBox.MinLon) / AssumedMapWidthPixels;
}
