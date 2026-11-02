using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapCensusTracts(IMapaRepository mapRepository)
{
    public async Task<string> ExecuteAsync(BoundingBox boundingBox, double? tolerance)
    {
        MapQueryPolicy.ValidateViewport(boundingBox);
        MapQueryPolicy.ValidateFeatureCount(await mapRepository.ContarRadiosAsync(boundingBox), "radios");

        var defaultTolerance = MapQueryPolicy.GetViewportTolerance(boundingBox);
        return await mapRepository.GetRadiosAsync(
            boundingBox,
            MapQueryPolicy.NormalizeTolerance(tolerance, defaultTolerance));
    }
}
