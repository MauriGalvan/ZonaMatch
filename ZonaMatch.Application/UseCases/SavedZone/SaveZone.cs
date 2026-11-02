using ZonaMatch.Application.DTOs.SavedZone.Requests;
using ZonaMatch.Application.DTOs.SavedZone.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Application.UseCases.SavedZone
{
    public class SaveZone
    {
        private readonly ISavedZoneRepository _savedZones;
        private readonly IZoneRepository _zones;
        private readonly TimeProvider _timeProvider;

        public SaveZone(ISavedZoneRepository savedZones, IZoneRepository zones, TimeProvider timeProvider)
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
                savedZone = new DomainSavedZone
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
