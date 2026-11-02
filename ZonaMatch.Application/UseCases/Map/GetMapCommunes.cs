using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Map;

public sealed class GetMapCommunes(IMapaRepository mapRepository)
{
    public Task<string> ExecuteAsync(double? tolerance) =>
        mapRepository.GetComunasAsync(MapQueryPolicy.NormalizeTolerance(tolerance));
}
