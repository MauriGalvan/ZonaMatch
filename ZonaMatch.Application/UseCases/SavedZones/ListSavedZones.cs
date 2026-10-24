using ZonaMatch.Application.DTOs.SavedZones.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedZones
{
    public class ListSavedZones
    {
        private readonly ISavedZoneRepository _savedZones;

        public ListSavedZones(ISavedZoneRepository savedZones)
        {
            _savedZones = savedZones;
        }

        public async Task<IReadOnlyList<SavedZoneResponse>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var zones = await _savedZones.GetForUserAsync(userId, cancellationToken);
            return zones.Select(zone => new SavedZoneResponse(
                zone.ZoneSlug, zone.Name, zone.Subtitle, zone.SavedAt)).ToList();
        }
    }
}
