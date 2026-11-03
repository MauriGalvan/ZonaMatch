using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class GroupDeletionApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task Group_WhenDeletedByOwner_DisappearsFromMembersAndRemovesDependentRecords()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var participant = await TestApiUser.RegisterAsync(factory);

        var createGroupResponse = await owner.Client.PostAsJsonAsync(
            "/api/v1/groups",
            new CreateGroupRequest { Name = "Group to delete" });
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GroupResponse>();
        group.Should().NotBeNull();

        var inviteLinkResponse = await owner.Client.GetAsync($"/api/v1/groups/{group!.Id}/invitation-link");
        inviteLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteLink = await inviteLinkResponse.Content.ReadFromJsonAsync<InviteLinkResponse>();
        inviteLink.Should().NotBeNull();

        var joinResponse = await participant.Client.PostAsync(
            $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
            content: null);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitation = new GroupInvitation
        {
            GroupId = group.Id,
            Email = $"pending-{Guid.NewGuid():N}@example.com",
            Token = Guid.NewGuid(),
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await using (var seedContext = database.CreateDbContext())
        {
            seedContext.GroupInvitations.Add(invitation);
            await seedContext.SaveChangesAsync();
        }

        foreach (var member in new[] { owner, participant })
        {
            var groupsResponse = await member.Client.GetAsync("/api/v1/groups");
            groupsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var groups = await groupsResponse.Content.ReadFromJsonAsync<GroupListResponse>();
            groups.Should().NotBeNull();
            groups!.Groups.Should().ContainSingle(item => item.Id == group.Id);

            var detailsResponse = await member.Client.GetAsync($"/api/v1/groups/{group.Id}");
            detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var deleteResponse = await owner.Client.DeleteAsync($"/api/v1/groups/{group.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        foreach (var member in new[] { owner, participant })
        {
            var groupsResponse = await member.Client.GetAsync("/api/v1/groups");
            groupsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var groups = await groupsResponse.Content.ReadFromJsonAsync<GroupListResponse>();
            groups.Should().NotBeNull();
            groups!.Groups.Should().NotContain(item => item.Id == group.Id);

            var detailsResponse = await member.Client.GetAsync($"/api/v1/groups/{group.Id}");
            detailsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var repeatedDeleteResponse = await owner.Client.DeleteAsync($"/api/v1/groups/{group.Id}");
        repeatedDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.Groups.AnyAsync(item => item.Id == group.Id)).Should().BeFalse();
        (await verificationContext.Set<Participant>().AnyAsync(item => item.GroupId == group.Id)).Should().BeFalse();
        (await verificationContext.GroupInvitations.AnyAsync(item => item.Id == invitation.Id)).Should().BeFalse();
    }

    private sealed record InviteLinkResponse(Guid InviteToken);
}
