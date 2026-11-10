using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FakeCommunityPoiRepository : ICommunityPoiRepository
{
    public List<CommunityPoi> Pois { get; } = [];

    public Task<CommunityPoi?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pois.FirstOrDefault(p => p.Id == id));

    public void Add(CommunityPoi poi) => Pois.Add(poi);
}
