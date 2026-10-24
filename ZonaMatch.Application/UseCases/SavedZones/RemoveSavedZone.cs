using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedZones
{
    public class RemoveSavedZone
    {
        private readonly ISavedZoneRepository _savedZones;

        public RemoveSavedZone(ISavedZoneRepository savedZones)
        {
            _savedZones = savedZones;
        }

        public async Task ExecuteAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default)
        {
            var savedZone = await _savedZones.GetAsync(userId, zoneSlug, cancellationToken);
            if (savedZone is null)
                return; // DELETE is idempotent when the user has not saved this zone.

            _savedZones.Remove(savedZone);
            await _savedZones.SaveChangesAsync(cancellationToken);
        }
    }
}
