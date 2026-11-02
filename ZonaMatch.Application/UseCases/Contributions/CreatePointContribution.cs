using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Contributions
{
    // Suggests a place missing from the map; it must be inside the zone.
    public class CreatePointContribution
    {
        private readonly IContributionRepository _contributions;
        private readonly IZoneRepository _zones;
        private readonly TimeProvider _clock;

        public CreatePointContribution(IContributionRepository contributions, IZoneRepository zones, TimeProvider clock)
        {
            _contributions = contributions;
            _zones = zones;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, CreatePointContributionRequest request, CancellationToken cancellationToken = default)
        {
            var category = ContributionMapper.ValidateCategory(request.Category);
            await _zones.EnsurePointBelongsToZoneAsync(zoneSlug, request.Latitude, request.Longitude, cancellationToken);

            var contribution = new Contribution
            {
                ZoneSlug = zoneSlug,
                UserId = currentUser.Id,
                Type = ContributionType.NewPoint,
                Category = category,
                Name = request.Name.Trim(),
                Location = ContributionMapper.CreatePoint(request.Latitude, request.Longitude),
                OpeningHours = ContributionMapper.Normalize(request.Schedule),
                Address = ContributionMapper.Normalize(request.Address),
                LivesOrWorksInZone = request.LivesOrWorksInZone,
                CreatedAt = _clock.GetUtcNow()
            };

            _contributions.Add(contribution);
            await _contributions.SaveChangesAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, currentUser.Email, currentUser.Id);
        }
    }
}
