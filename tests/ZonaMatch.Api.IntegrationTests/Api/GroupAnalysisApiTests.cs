using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Requests;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class GroupAnalysisApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task GroupAnalysis_WhenCalculatedAndSaved_UsesAllMemberProfilesAndRestrictsAccess()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        using var guest = await TestApiUser.RegisterAsync(factory);
        using var outsider = await TestApiUser.RegisterAsync(factory);

        var createGroupResponse = await owner.Client.PostAsJsonAsync(
            "/api/v1/groups",
            new CreateGroupRequest { Name = "Group ranking test" });
        createGroupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GroupResponse>();
        group.Should().NotBeNull();

        var inviteLinkResponse = await owner.Client.GetAsync($"/api/v1/groups/{group!.Id}/invitation-link");
        inviteLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteLink = await inviteLinkResponse.Content.ReadFromJsonAsync<InviteLinkResponse>();
        inviteLink.Should().NotBeNull();
        var joinResponse = await guest.Client.PostAsync(
            $"/api/v1/group-invitation-links/{inviteLink!.InviteToken}/acceptances",
            content: null);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerProfile = CreateMemberProfile(
            personName: "Ana",
            ageRange: "6-11",
            criterion: "seguridad",
            latitude: -34.61,
            longitude: -58.41);
        var guestProfile = CreateMemberProfile(
            personName: "Leo",
            ageRange: "12-17",
            criterion: "salud",
            latitude: -34.62,
            longitude: -58.42);

        (await owner.Client.PutAsJsonAsync("/api/v1/profile", ownerProfile)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await guest.Client.PutAsJsonAsync("/api/v1/profile", guestProfile)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var groupDetailsResponse = await owner.Client.GetAsync($"/api/v1/groups/{group.Id}");
        groupDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var groupDetails = await groupDetailsResponse.Content.ReadFromJsonAsync<GroupDetailsResponse>();
        groupDetails.Should().NotBeNull();
        var ownerParticipant = groupDetails!.Participants.Single(participant => participant.UserId == owner.User.Id);
        var guestParticipant = groupDetails.Participants.Single(participant => participant.UserId == guest.User.Id);
        var weights = new List<ParticipantWeightRequest>
        {
            new() { ParticipantId = ownerParticipant.Id, Weight = 80 },
            new() { ParticipantId = guestParticipant.Id, Weight = 20 }
        };

        var rankingRequest = new CalculateGroupScoreRequest { ParticipantWeights = weights };
        var rankingResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/rankings",
            rankingRequest);
        rankingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ranking = await rankingResponse.Content.ReadFromJsonAsync<GroupRankingResponse>();

        ranking.Should().NotBeNull();
        ranking!.Zones.Should().HaveCount(2);
        ranking.Zones[0].Slug.Should().Be(IntegrationZoneRepository.SafetyZoneSlug);
        ranking.Zones[0].Score.Should().NotBeNull();
        ranking.Zones[0].Participants.Should().HaveCount(2);
        ranking.Zones[0].Participants.Single(participant => participant.ParticipantId == ownerParticipant.Id)
            .NormalizedWeight.Should().Be(80);
        ranking.Zones[0].Participants.Single(participant => participant.ParticipantId == guestParticipant.Id)
            .NormalizedWeight.Should().Be(20);
        ranking.ParticipantPoints.Should().HaveCount(2);
        ranking.ProfileRecommendations.Should().NotBeNull();
        ranking.ProfileRecommendations!.AgeRanges.Should().Equal("6-11", "12-17");
        ranking.ProfileRecommendations.HasPets.Should().BeTrue();
        ranking.ProfileRecommendations.HasBicycle.Should().BeFalse();

        var guestRankingResponse = await guest.Client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/rankings",
            rankingRequest);
        guestRankingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var outsiderRankingResponse = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/rankings",
            rankingRequest);
        outsiderRankingResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var saveRequest = new SaveGroupAnalysisRequest
        {
            Name = "  Shared group ranking  ",
            Zones =
            [
                new AnalysisZoneRequest
                {
                    Slug = ranking.Zones[0].Slug,
                    Name = ranking.Zones[0].Name,
                    IsInitial = true
                }
            ],
            ParticipantWeights = weights
        };
        var saveResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/analyses",
            saveRequest);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var savedAnalysis = await saveResponse.Content.ReadFromJsonAsync<AnalysisResponse>();

        savedAnalysis.Should().NotBeNull();
        var savedGroupAnalysis = savedAnalysis!;
        savedGroupAnalysis.Name.Should().Be("Shared group ranking");
        savedGroupAnalysis.Zones.Should().ContainSingle(zone => zone.IsInitial)
            .Which.Slug.Should().Be(IntegrationZoneRepository.SafetyZoneSlug);
        savedGroupAnalysis.GroupScores.Should().NotBeNull();
        savedGroupAnalysis.GroupScores!.Zones.Should().HaveCount(2);
        savedGroupAnalysis.GroupScores.Zones[0].Slug.Should().Be(IntegrationZoneRepository.SafetyZoneSlug);
        savedGroupAnalysis.Context.PersonNames.Should().ContainSingle().Which.Should().Be("Ana");

        var savedAnalysisResponse = await owner.Client.GetAsync($"/api/v1/analyses/{savedGroupAnalysis.Id}");
        savedAnalysisResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedAnalysisDetails = await savedAnalysisResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        savedAnalysisDetails.Should().NotBeNull();
        savedAnalysisDetails!.GroupScores.Should().NotBeNull();
        savedAnalysisDetails.GroupScores!.Zones.Should().HaveCount(2);
        savedAnalysisDetails.GroupScores.Zones[0].Score.Should().Be(savedGroupAnalysis.GroupScores.Zones[0].Score);

        var guestSavedAnalysisResponse = await guest.Client.GetAsync($"/api/v1/analyses/{savedGroupAnalysis.Id}");
        guestSavedAnalysisResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var outsiderSaveResponse = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/analyses",
            saveRequest);
        outsiderSaveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var moveGroupAnalysisToTrashResponse = await owner.Client.DeleteAsync(
            $"/api/v1/analyses/{savedGroupAnalysis.Id}");
        moveGroupAnalysisToTrashResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var groupTrashResponse = await owner.Client.GetAsync("/api/v1/analyses/trash");
        var groupTrash = await groupTrashResponse.Content.ReadFromJsonAsync<IReadOnlyList<TrashedAnalysisResponse>>();
        groupTrashResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trashedGroupAnalysis = groupTrash!.Should().ContainSingle().Which;
        trashedGroupAnalysis.Id.Should().Be(savedGroupAnalysis.Id);
        trashedGroupAnalysis.Name.Should().Be(savedGroupAnalysis.Name);
        trashedGroupAnalysis.IsGroupAnalysis.Should().BeTrue();

        var restoreGroupAnalysisResponse = await owner.Client.PostAsync(
            $"/api/v1/analyses/trash/{savedGroupAnalysis.Id}/restore",
            content: null);
        restoreGroupAnalysisResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var restoredGroupAnalysisResponse = await owner.Client.GetAsync($"/api/v1/analyses/{savedGroupAnalysis.Id}");
        restoredGroupAnalysisResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var restoredGroupAnalysis = await restoredGroupAnalysisResponse.Content.ReadFromJsonAsync<AnalysisResponse>();
        restoredGroupAnalysis.Should().NotBeNull();
        restoredGroupAnalysis!.GroupScores.Should().NotBeNull();
    }

    private static AnalysisCriteriaRequest CreateMemberProfile(
        string personName,
        string ageRange,
        string criterion,
        double latitude,
        double longitude)
    {
        var profile = ProfileApiTests.CreateProfileRequest(
            peopleCount: 1,
            personNames: [personName],
            ageRanges: [ageRange],
            criterion: criterion,
            priority: "alta",
            placeName: $"{personName}'s school");
        profile.Points.Places[0].Latitude = latitude;
        profile.Points.Places[0].Longitude = longitude;
        return profile;
    }

    private sealed record InviteLinkResponse(Guid InviteToken);
}
