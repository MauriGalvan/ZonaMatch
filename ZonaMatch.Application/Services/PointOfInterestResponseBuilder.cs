using ZonaMatch.Application.DTOs;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.Services;

/// <summary>
/// Shared response shaping for point-of-interest queries by location and by zone.
/// </summary>
public sealed class PointOfInterestResponseBuilder
{
    // Covers large neighborhoods while keeping GeoJSON and map payloads manageable.
    public const int MaximumPoints = 5_000;

    // The extra row indicates whether the repository result was truncated.
    public PuntosInteresCercanosDto BuildPoints(IReadOnlyList<PuntoInteresDto> points) =>
        points.Count > MaximumPoints
            ? new PuntosInteresCercanosDto(points.Take(MaximumPoints).ToList(), Truncado: true)
            : new PuntosInteresCercanosDto(points, Truncado: false);

    public ResumenPuntosInteresDto BuildSummary(
        double? radiusMeters,
        IReadOnlyList<CantidadPorTipo> counts) =>
        new(radiusMeters, BuildCategories(counts));

    private static IReadOnlyList<ResumenCategoriaDto> BuildCategories(IReadOnlyList<CantidadPorTipo> counts) =>
        CategoriaPuntoInteres.Todas
            .Select(category =>
            {
                var types = counts
                    .Where(count => count.Categoria == category)
                    .OrderByDescending(count => count.Cantidad)
                    .Select(count => new CantidadPorTipoDto(count.Tipo, count.Cantidad))
                    .ToList();

                return new ResumenCategoriaDto(category, types.Sum(type => type.Cantidad), types);
            })
            .ToList();
}
