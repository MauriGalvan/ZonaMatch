using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// Names the admin moderation lists show: zones by slug and the administrators who resolved something.
internal static class AdminLookups
{
    // Zone names by slug; a zone without data is shown by its slug.
    public static async Task<IReadOnlyDictionary<string, string>> ZoneNamesAsync(
        IZoneProfilesRepository zones, CancellationToken cancellationToken) =>
        (await zones.ListAsync(cancellationToken))
            .GroupBy(zone => zone.Slug)
            .ToDictionary(group => group.Key, group => group.First().Name);

    // Emails of some users. Only a few administrators resolve contributions, so they are read one by one.
    public static async Task<IReadOnlyDictionary<Guid, string?>> EmailsAsync(
        IUserRepository users, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var emails = new Dictionary<Guid, string?>();
        foreach (var userId in userIds.Distinct())
            emails[userId] = (await users.GetByIdAsync(userId, cancellationToken))?.Email;
        return emails;
    }
}
