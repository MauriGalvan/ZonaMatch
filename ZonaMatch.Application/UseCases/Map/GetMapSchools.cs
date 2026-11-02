using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapSchools(IMapRepository mapRepository)
{
    public async Task<string> ExecuteAsync(BoundingBox boundingBox, string? level, string? sector)
    {
        MapQueryPolicy.ValidateViewport(boundingBox);
        MapQueryPolicy.ValidateFeatureCount(
            await mapRepository.CountSchoolsAsync(boundingBox, level, sector), "escuelas");

        return await mapRepository.GetSchoolsAsync(boundingBox, level, sector);
    }
}
