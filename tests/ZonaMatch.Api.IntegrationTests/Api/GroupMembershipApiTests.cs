using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class GroupMembershipApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task GroupLink_WhenGroupIsFull_RejectsAnotherMemberAndHidesDetailsFromNonMembers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var firstGuest = await TestApiUser.RegisterAsync(factory);
        using var secondGuest = await TestApiUser.RegisterAsync(factory);
        using var thirdGuest = await TestApiUser.RegisterAsync(factory);
        using var fourthGuest = await TestApiUser.RegisterAsync(factory);
        using var outsider = await TestApiUser.RegisterAsync(factory);

        var createGroupResponse = await owner.Client.PostAsJsonAsync(
            "/api/v1/groups",
            new CreateGroupRequest { Name = "Integration group" });

        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GroupResponse>();
        group.Should().NotBeNull();
        group!.IsOwner.Should().BeTrue();

        var inviteLinkResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}/invitation-link");
        inviteLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteLink = await inviteLinkResponse.Content.ReadFromJsonAsync<InviteLinkResponse>();
        inviteLink.Should().NotBeNull();

        foreach (var guest in new[] { firstGuest, secondGuest, thirdGuest, fourthGuest })
        {
            var joinResponse = await guest.Client.PostAsync(
                $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
                content: null);

            joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var fullGroupDetails = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}");
        fullGroupDetails.StatusCode.Should().Be(HttpStatusCode.OK);
        var details = await fullGroupDetails.Content.ReadFromJsonAsync<GroupDetailsResponse>();
        details.Should().NotBeNull();
        details!.Participants.Should().HaveCount(5);

        var fullGroupJoinResponse = await outsider.Client.PostAsync(
            $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
            content: null);
        fullGroupJoinResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var outsiderGroupDetails = await outsider.Client.GetAsync($"/api/v1/groups/{group.Id}");
        outsiderGroupDetails.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Group_WhenGuestTriesOwnerActions_RejectsThemAndAllowsOwnerToManageMembers()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var guest = await TestApiUser.RegisterAsync(factory);
        using var guestToRemove = await TestApiUser.RegisterAsync(factory);

        var createGroupResponse = await owner.Client.PostAsJsonAsync(
            "/api/v1/groups",
            new CreateGroupRequest { Name = "Original group name" });
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GroupResponse>();
        group.Should().NotBeNull();

        var inviteLinkResponse = await owner.Client.GetAsync($"/api/v1/groups/{group!.Id}/invitation-link");
        inviteLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteLink = await inviteLinkResponse.Content.ReadFromJsonAsync<InviteLinkResponse>();
        inviteLink.Should().NotBeNull();

        foreach (var invitedUser in new[] { guest, guestToRemove })
        {
            var joinResponse = await invitedUser.Client.PostAsync(
                $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
                content: null);
            joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await guest.Client.GetAsync($"/api/v1/groups/{group.Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await guest.Client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}",
            new UpdateGroupRequest { Name = "Unauthorized rename" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await guest.Client.DeleteAsync($"/api/v1/groups/{group.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await guest.Client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/participants/{guestToRemove.User.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await guest.Client.PutAsync($"/api/v1/groups/{group.Id}/invitation-link", content: null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        var renameResponse = await owner.Client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}",
            new UpdateGroupRequest { Name = "Owner renamed group" });
        renameResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removeGuestResponse = await owner.Client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/participants/{guestToRemove.User.Id}");
        removeGuestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updatedGroupResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}");
        updatedGroupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedGroup = await updatedGroupResponse.Content.ReadFromJsonAsync<GroupDetailsResponse>();
        updatedGroup.Should().NotBeNull();
        updatedGroup!.Name.Should().Be("Owner renamed group");
        updatedGroup.Participants.Should().HaveCount(2);

        var removedGuestDetails = await guestToRemove.Client.GetAsync($"/api/v1/groups/{group.Id}");
        removedGuestDetails.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record InviteLinkResponse(Guid InviteToken);
}
