using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapDistricts(IMapRepository mapRepository)
{
    public Task<string> ExecuteAsync(double? tolerance) =>
        mapRepository.GetDistrictsAsync(MapQueryPolicy.NormalizeTolerance(tolerance));
}
