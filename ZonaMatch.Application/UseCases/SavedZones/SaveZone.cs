using ZonaMatch.Application.DTOs.SavedZones.Requests;
using ZonaMatch.Application.DTOs.SavedZones.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedZones
{
    public class SaveZone
    {
        private readonly ISavedZoneRepository _savedZones;
        private readonly IZonaRepository _zones;
        private readonly TimeProvider _timeProvider;

        public SaveZone(ISavedZoneRepository savedZones, IZonaRepository zones, TimeProvider timeProvider)
        {
            _savedZones = savedZones;
            _zones = zones;
            _timeProvider = timeProvider;
        }

        public async Task<SavedZoneResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext user, SaveZoneRequest request, CancellationToken cancellationToken = default)
        {
            if (!await _zones.ExistsAsync(zoneSlug, cancellationToken))
                throw new NotFoundException("Zone not found.");

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new BadRequestException("Zone name is required.");

            var savedZone = await _savedZones.GetAsync(user.Id, zoneSlug, cancellationToken);
            if (savedZone is null)
            {
                savedZone = new SavedZone
                {
                    UserId = user.Id,
                    ZoneSlug = zoneSlug,
                    SavedAt = _timeProvider.GetUtcNow()
                };
                _savedZones.Add(savedZone);
            }

            savedZone.Name = name;
            savedZone.Subtitle = request.Subtitle.Trim();
            await _savedZones.SaveChangesAsync(cancellationToken);

            return new SavedZoneResponse(savedZone.ZoneSlug, savedZone.Name, savedZone.Subtitle, savedZone.SavedAt);
        }
    }
}
