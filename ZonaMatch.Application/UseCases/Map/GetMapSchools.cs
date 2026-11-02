using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapSchools(IMapaRepository mapRepository)
{
    public async Task<string> ExecuteAsync(BoundingBox boundingBox, string? level, string? sector)
    {
        MapQueryPolicy.ValidateViewport(boundingBox);
        MapQueryPolicy.ValidateFeatureCount(
            await mapRepository.ContarEscuelasAsync(boundingBox, level, sector), "escuelas");

        return await mapRepository.GetEscuelasAsync(boundingBox, level, sector);
    }
}
