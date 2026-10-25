using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Points added or changed by the community (app.community_pois). Changes are saved with the contribution
    // that produced them (IAporteRepository.GuardarCambiosAsync shares the unit of work).
    public interface ICommunityPoiRepository
    {
        // Tracked, to apply further corrections
        Task<CommunityPoi?> GetAsync(string id, CancellationToken cancellationToken = default);

        void Add(CommunityPoi poi);
    }
}
