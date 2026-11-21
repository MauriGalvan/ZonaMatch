using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetAvailableZones(IZoneRepository zoneRepository)
{
    public Task<IReadOnlyList<AvailableZoneResponse>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        zoneRepository.ListAvailableAsync(cancellationToken);
}
