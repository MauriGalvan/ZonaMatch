using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapDistricts(IMapaRepository mapRepository)
{
    public Task<string> ExecuteAsync(double? tolerance) =>
        mapRepository.GetPartidosAsync(MapQueryPolicy.NormalizeTolerance(tolerance));
}
