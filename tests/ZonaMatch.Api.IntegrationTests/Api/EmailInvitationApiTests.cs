using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Application.DTOs.GroupInvitation.Responses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class EmailInvitationApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task EmailInvitation_WhenPendingAndValid_AddsRecipientAndCannotBeAcceptedTwice()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var recipient = await TestApiUser.RegisterAsync(factory);
        using var expiredRecipient = await TestApiUser.RegisterAsync(factory);

        var group = await CreateGroupAsync(owner.Client);
        var validToken = Guid.NewGuid();
        var expiredToken = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var dbContext = database.CreateDbContext())
        {
            dbContext.GroupInvitations.AddRange(
                new GroupInvitation
                {
                    GroupId = group.Id,
                    Email = recipient.User.Email,
                    Token = validToken,
                    Status = InvitationStatus.Pending,
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(2)
                },
                new GroupInvitation
                {
                    GroupId = group.Id,
                    Email = expiredRecipient.User.Email,
                    Token = expiredToken,
                    Status = InvitationStatus.Pending,
                    CreatedAt = now.AddDays(-8),
                    ExpiresAt = now.AddDays(-1)
                });
            await dbContext.SaveChangesAsync();
        }

        var invitationDetailsResponse = await owner.Client.GetAsync($"/api/v1/group-invitations/email/{validToken}");
        invitationDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitationDetails = await invitationDetailsResponse.Content.ReadFromJsonAsync<InvitationDetailsResponse>();
        invitationDetails.Should().NotBeNull();
        invitationDetails!.GroupId.Should().Be(group.Id);
        invitationDetails.GroupName.Should().Be(group.Name);
        invitationDetails.Status.Should().Be(nameof(InvitationStatus.Pending));

        var acceptResponse = await recipient.Client.PostAsync(
            $"/api/v1/group-invitations/email/{validToken}/acceptances",
            content: null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var acceptedDetailsResponse = await owner.Client.GetAsync($"/api/v1/group-invitations/email/{validToken}");
        var acceptedDetails = await acceptedDetailsResponse.Content.ReadFromJsonAsync<InvitationDetailsResponse>();
        acceptedDetails.Should().NotBeNull();
        acceptedDetails!.Status.Should().Be(nameof(InvitationStatus.Accepted));

        var repeatedAcceptResponse = await recipient.Client.PostAsync(
            $"/api/v1/group-invitations/email/{validToken}/acceptances",
            content: null);
        repeatedAcceptResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var groupDetailsResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}");
        var groupDetails = await groupDetailsResponse.Content.ReadFromJsonAsync<GroupDetailsResponse>();
        groupDetails.Should().NotBeNull();
        groupDetails!.Participants.Should().ContainSingle(participant => participant.UserId == recipient.User.Id);

        var expiredAcceptResponse = await expiredRecipient.Client.PostAsync(
            $"/api/v1/group-invitations/email/{expiredToken}/acceptances",
            content: null);
        expiredAcceptResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var expiredDetailsResponse = await owner.Client.GetAsync($"/api/v1/group-invitations/email/{expiredToken}");
        var expiredDetails = await expiredDetailsResponse.Content.ReadFromJsonAsync<InvitationDetailsResponse>();
        expiredDetails.Should().NotBeNull();
        expiredDetails!.Status.Should().Be(nameof(InvitationStatus.Expired));

        await using var verificationContext = database.CreateDbContext();
        (await verificationContext.GroupInvitations.CountAsync(invitation =>
            invitation.GroupId == group.Id && invitation.Status == InvitationStatus.Accepted)).Should().Be(1);
        (await verificationContext.GroupInvitations.CountAsync(invitation =>
            invitation.GroupId == group.Id && invitation.Status == InvitationStatus.Expired)).Should().Be(1);
    }

    [Fact]
    public async Task EmailInvitation_WhenGroupIsFull_StaysPendingAndDoesNotAddRecipient()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var firstGuest = await TestApiUser.RegisterAsync(factory);
        using var secondGuest = await TestApiUser.RegisterAsync(factory);
        using var thirdGuest = await TestApiUser.RegisterAsync(factory);
        using var fourthGuest = await TestApiUser.RegisterAsync(factory);
        using var recipient = await TestApiUser.RegisterAsync(factory);

        var group = await CreateGroupAsync(owner.Client);
        var inviteLinkResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}/invitation-link");
        var inviteLink = await inviteLinkResponse.Content.ReadFromJsonAsync<InviteLinkResponse>();
        inviteLink.Should().NotBeNull();

        foreach (var guest in new[] { firstGuest, secondGuest, thirdGuest, fourthGuest })
        {
            var joinResponse = await guest.Client.PostAsync(
                $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
                content: null);
            joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var token = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using (var dbContext = database.CreateDbContext())
        {
            dbContext.GroupInvitations.Add(new GroupInvitation
            {
                GroupId = group.Id,
                Email = recipient.User.Email,
                Token = token,
                Status = InvitationStatus.Pending,
                CreatedAt = now,
                ExpiresAt = now.AddDays(2)
            });
            await dbContext.SaveChangesAsync();
        }

        var acceptResponse = await recipient.Client.PostAsync(
            $"/api/v1/group-invitations/email/{token}/acceptances",
            content: null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var invitationResponse = await owner.Client.GetAsync($"/api/v1/group-invitations/email/{token}");
        var invitation = await invitationResponse.Content.ReadFromJsonAsync<InvitationDetailsResponse>();
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be(nameof(InvitationStatus.Pending));

        var groupDetailsResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}");
        var groupDetails = await groupDetailsResponse.Content.ReadFromJsonAsync<GroupDetailsResponse>();
        groupDetails.Should().NotBeNull();
        groupDetails!.Participants.Should().HaveCount(5)
            .And.NotContain(participant => participant.UserId == recipient.User.Id);
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient ownerClient)
    {
        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/groups",
            new CreateGroupRequest { Name = "Email invitation group" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private sealed record InviteLinkResponse(Guid InviteToken);
}
