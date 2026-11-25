using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetZoneCoverage(IZoneRepository zoneRepository)
{
    public Task<string> ExecuteAsync(
        bool simplified = false,
        double? tolerance = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTolerance = ZoneQueryPolicy.NormalizeCoverageTolerance(tolerance, simplified);
        return zoneRepository.GetCoverageGeoJsonAsync(normalizedTolerance, cancellationToken);
    }
}
