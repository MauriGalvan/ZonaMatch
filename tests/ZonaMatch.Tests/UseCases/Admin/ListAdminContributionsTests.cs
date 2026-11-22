using FluentAssertions;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Tests.UseCases.Community;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;

namespace ZonaMatch.Tests.UseCases.Admin;

public class ListAdminContributionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.Zero);

    private readonly User _author = new() { Email = "ana@example.com" };
    private readonly User _administrator = new() { Email = "admin@example.com", IsAdministrator = true };
    private readonly FakeContributionRepository _contributions = new();
    private readonly ListAdminContributions _useCase;

    public ListAdminContributionsTests()
    {
        _useCase = new ListAdminContributions(
            _contributions,
            new FakeAdminUserRepository(_author, _administrator),
            new FakeAdminZoneProfilesRepository(new ZoneProfile { Slug = "palermo", Name = "Palermo" }));
    }

    private DomainContribution Add(string name, DateTimeOffset createdAt, ContributionType type = ContributionType.NewPoint)
    {
        var contribution = new DomainContribution
        {
            ZoneSlug = "palermo",
            UserId = _author.Id,
            User = _author,
            Type = type,
            Name = name,
            Category = "plaza",
            CreatedAt = createdAt,
        };
        _contributions.Add(contribution);
        return contribution;
    }

    [Fact]
    public async Task ListContributions_SplitsPendingFromTheHistory()
    {
        Add("Plaza nueva", Now.AddHours(-2));
        Add("Plaza aprobada", Now.AddHours(-5)).Resolve(_administrator.Id, approve: true, Now.AddHours(-1));

        var result = await _useCase.ExecuteAsync();

        result.Pending.Should().ContainSingle().Which.Name.Should().Be("Plaza nueva");
        var resolved = result.History.Should().ContainSingle().Subject;
        resolved.Name.Should().Be("Plaza aprobada");
        resolved.Status.Should().Be("approved");
        resolved.ResolvedByEmail.Should().Be("admin@example.com");
        resolved.ZoneName.Should().Be("Palermo");
        resolved.AuthorEmail.Should().Be("ana@example.com");
    }

    [Fact]
    public async Task ListContributions_WhenTheCommunityResolvedIt_HasNoAdministrator()
    {
        var contribution = Add("Votada", Now.AddDays(-1));
        for (var vote = 0; vote < DomainContribution.VoteDifferenceToResolve; vote++)
            contribution.RecordVote(Guid.NewGuid(), confirms: false, Now);

        var resolved = (await _useCase.ExecuteAsync()).History.Single();

        resolved.Status.Should().Be("rejected");
        resolved.ResolvedAt.Should().Be(Now);
        resolved.ResolvedByEmail.Should().BeNull();
        resolved.Rejections.Should().Be(DomainContribution.VoteDifferenceToResolve);
    }

    [Fact]
    public async Task ListContributions_DescribesCorrectionsWithTheirReason()
    {
        var correction = Add("Nombre nuevo", Now, ContributionType.Correction);
        correction.PointOfInterestName = "Nombre viejo";
        correction.Reason = CorrectionReason.Name;

        var item = (await _useCase.ExecuteAsync()).Pending.Single();

        item.Type.Should().Be("correction");
        item.Reason.Should().Be("name");
        item.PointOfInterestName.Should().Be("Nombre viejo");
    }
}
