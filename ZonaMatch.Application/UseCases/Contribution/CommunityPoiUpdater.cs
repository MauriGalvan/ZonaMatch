using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Application.UseCases.Contribution
{
    // Persists an approved contribution as a point of interest: a new point is added, a correction changes the
    // name, category or location of the place, or removes it if it closed. Shared by the community vote
    // (ValidateContribution) and the administrator (ResolveContribution). The caller saves the changes.
    public class CommunityPoiUpdater
    {
        private readonly ICommunityPoiRepository _pois;

        public CommunityPoiUpdater(ICommunityPoiRepository pois)
        {
            _pois = pois;
        }

        public async Task ApplyAsync(DomainContribution contribution, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            if (contribution.Status != ContributionStatus.Approved)
                return;

            if (contribution.Type == ContributionType.NewPoint)
            {
                if (await _pois.GetAsync(CommunityPoi.NewPointId(contribution.Id), cancellationToken) is null)
                    _pois.Add(CommunityPoi.FromNewPoint(contribution, now));
                return;
            }

            if (contribution.PointOfInterestId is not { } poiId || contribution.Reason == CorrectionReason.Other)
                return;

            var poi = await _pois.GetAsync(poiId, cancellationToken);
            if (poi is null)
            {
                poi = CommunityPoi.ForOsmPoint(poiId, contribution.ZoneSlug);
                _pois.Add(poi);
            }

            poi.ApplyCorrection(contribution, now);
        }
    }
}
