using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapCommunes(IMapRepository mapRepository)
{
    public Task<string> ExecuteAsync(double? tolerance) =>
        mapRepository.GetCommunesAsync(MapQueryPolicy.NormalizeTolerance(tolerance));
}
