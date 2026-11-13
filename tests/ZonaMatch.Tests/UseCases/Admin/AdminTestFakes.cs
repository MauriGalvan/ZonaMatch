using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Admin;

// Returns a fixed snapshot and records the period the use case asked for.
internal sealed class FakeAdminStatsRepository(params DateTimeOffset[] registrationDates) : IAdminStatsRepository
{
    public static readonly AdminUserStats Users = new(Total: 10, NewLast7Days: 2, NewLast30Days: 5, Administrators: 1, WithProfile: 7, WithAnalysis: 4);

    public AdminStatsPeriod? RequestedPeriod { get; private set; }

    public Task<AdminStatsSnapshot> GetSnapshotAsync(AdminStatsPeriod period, CancellationToken cancellationToken = default)
    {
        RequestedPeriod = period;
        return Task.FromResult(new AdminStatsSnapshot(
            Users,
            new AdminActivityStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new AdminCommunityStats(0, 0, null, 0, 0, 0, 0, 0, 0, 0),
            registrationDates,
            [new AdminZoneCount("palermo", "Palermo", 3)],
            [],
            [new AdminCriterionCount("seguridad", "Seguridad", 4, 2)],
            new AdminOperationSplit(Rent: 3, Purchase: 1)));
    }
}

// In-memory user store for the admin use cases. Users are stored by reference, so tests can check the persisted values.
internal sealed class FakeAdminUserRepository(params User[] users) : IUserRepository
{
    public List<User> Users { get; } = [.. users];

    public int SetAdministratorCalls { get; private set; }

    // Returns the users in insertion order, so tests can check that the use case keeps the repository order.
    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users.ToList());

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

    public Task SetAdministratorAsync(Guid id, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        SetAdministratorCalls++;
        Users.Single(user => user.Id == id).IsAdministrator = isAdministrator;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAdministratorEmailsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Users
            .Where(user => user.IsAdministrator)
            .Select(user => user.Email)
            .ToList());

    public Task<IReadOnlyList<Guid>> GetAdministratorIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(Users
            .Where(user => user.IsAdministrator)
            .Select(user => user.Id)
            .ToList());

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

// Fixed zone profiles for the admin zone use cases.
internal sealed class FakeAdminZoneProfilesRepository(params ZoneProfile[] zones) : IZoneProfilesRepository
{
    public Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ZoneProfile>>(zones);

    public Task InvalidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
        IReadOnlyCollection<(double Latitude, double Longitude)> locations,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(zones.Select(zone => zone.Slug).ToArray());
}
