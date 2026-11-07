using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Application.UseCases.Contribution
{
    // Suggests a map correction when a place closed or its name, location, or category is incorrect.
    public class CreateContributionCorrection
    {
        private readonly IContributionRepository _contributions;
        private readonly IZoneRepository _zones;
        private readonly TimeProvider _clock;

        public CreateContributionCorrection(IContributionRepository contributions, IZoneRepository zones, TimeProvider clock)
        {
            _contributions = contributions;
            _zones = zones;
            _clock = clock;
        }

        public async Task<ContributionResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, CreateContributionCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            var reason = request.Reason!.Value; // Required by request validation.

            var contribution = new DomainContribution
            {
                ZoneSlug = zoneSlug,
                UserId = currentUser.Id,
                Type = ContributionType.Correction,
                PointOfInterestId = request.PointOfInterestId,
                PointOfInterestName = request.PointOfInterestName.Trim(),
                Reason = reason,
                Comment = ContributionMapper.Normalize(request.Comment),
                CreatedAt = _clock.GetUtcNow()
            };

            // Store only the value associated with the selected reason.
            switch (reason)
            {
                case CorrectionReason.Name:
                    contribution.Name = ContributionMapper.Normalize(request.ProposedName)
                        ?? throw new BadRequestException("Indicá el nombre correcto del lugar.");
                    break;

                case CorrectionReason.Category:
                    contribution.Category = ContributionMapper.ValidateCategory(request.ProposedCategory);
                    break;

                case CorrectionReason.Location:
                    if (request.Latitude is not { } latitude || request.Longitude is not { } longitude)
                        throw new BadRequestException("Marcá en el mapa la ubicación correcta del lugar.");
                    await _zones.EnsurePointBelongsToZoneAsync(zoneSlug, latitude, longitude, cancellationToken);
                    contribution.Location = ContributionMapper.CreatePoint(latitude, longitude);
                    break;

                case CorrectionReason.Other when contribution.Comment is null:
                    throw new BadRequestException("Contanos que hay que corregir en el comentario.");
            }

            // Location validation above already checks the zone; other reasons are validated here.
            if (reason != CorrectionReason.Location)
                await _zones.EnsureZoneExistsAsync(zoneSlug, cancellationToken);

            _contributions.Add(contribution);
            await _contributions.SaveChangesAsync(cancellationToken);

            return ContributionMapper.ToResponse(contribution, currentUser.Email, currentUser.Id);
        }
    }
}
