using FluentAssertions;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.UseCases.Admin;

namespace ZonaMatch.Tests.UseCases.Admin;

public class ListAdminGroupsTests
{
    private static AdminGroupResponse Group(string name, DateTimeOffset createdAt, params AdminGroupMemberResponse[] members) =>
        new(Guid.NewGuid(), name, createdAt, members);

    private static AdminGroupMemberResponse Member(string email, bool isOwner = false) => new(Guid.NewGuid(), email, isOwner);

    [Fact]
    public async Task ListGroups_ReturnsTheNewestGroupFirst()
    {
        var repository = new FakeAdminStatsRepository();
        repository.Groups.AddRange([
            Group("Familia", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
            Group("Amigos", new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero)),
            Group("Trabajo", new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero))]);

        var groups = await new ListAdminGroups(repository).ExecuteAsync();

        groups.Select(group => group.Name).Should().Equal("Amigos", "Trabajo", "Familia");
    }

    [Fact]
    public async Task ListGroups_ListsTheOwnerFirstAndThenTheMembersByEmail()
    {
        var repository = new FakeAdminStatsRepository();
        repository.Groups.Add(Group(
            "Amigos",
            new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
            Member("carla@example.com"), Member("zoe@example.com", isOwner: true), Member("ana@example.com")));

        var group = (await new ListAdminGroups(repository).ExecuteAsync()).Single();

        group.Members.Select(member => member.Email).Should().Equal("zoe@example.com", "ana@example.com", "carla@example.com");
        group.Members[0].IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task ListGroups_WhenThereAreNoGroups_ReturnsAnEmptyList()
    {
        var groups = await new ListAdminGroups(new FakeAdminStatsRepository()).ExecuteAsync();

        groups.Should().BeEmpty();
    }
}
