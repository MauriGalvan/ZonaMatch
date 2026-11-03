using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Profile.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ProfileApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task SaveProfile_WhenUpdated_ReplacesAndReturnsNestedContextAndCriteria()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var user = await TestApiUser.RegisterAsync(factory);

        var createResponse = await user.Client.PutAsJsonAsync("/api/v1/profile", CreateProfileRequest(
            peopleCount: 2,
            personNames: [" Ana ", " Mateo "],
            ageRanges: ["32-39", "6-11"],
            criterion: "seguridad",
            priority: "alta",
            placeName: "Trabajo"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateResponse = await user.Client.PutAsJsonAsync("/api/v1/profile", CreateProfileRequest(
            peopleCount: 1,
            personNames: ["Ana"],
            ageRanges: ["32-39"],
            criterion: "salud",
            priority: "media",
            placeName: "Universidad"));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await user.Client.GetAsync("/api/v1/profile");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await getResponse.Content.ReadFromJsonAsync<ProfileResponse>();

        profile.Should().NotBeNull();
        profile!.Context.PeopleCount.Should().Be(1);
        profile.Context.PersonNames.Should().Equal("Ana");
        profile.Context.AgeRanges.Should().Equal("32-39");
        profile.Context.Mobility.Should().Equal("transporte-publico");
        profile.Context.Pets.Should().Equal("gato");
        profile.Criteria.Should().ContainKey("salud").WhoseValue.Should().Be("media");
        profile.Criteria.Should().NotContainKey("seguridad");
        profile.Points.RentLimit.Should().Be("800000");
        profile.Points.Places.Should().ContainSingle()
            .Which.Name.Should().Be("Universidad");
    }

    internal static AnalysisCriteriaRequest CreateProfileRequest(
        int peopleCount,
        List<string?> personNames,
        List<string?> ageRanges,
        string criterion,
        string priority,
        string placeName) => new()
    {
        Context = new AnalysisContextRequest
        {
            PeopleCount = peopleCount,
            PersonNames = personNames,
            AgeRanges = ageRanges,
            Mobility = ["transporte-publico"],
            Pets = ["gato"]
        },
        Criteria = new Dictionary<string, string?> { [criterion] = priority },
        Points = new AnalysisPointsRequest
        {
            RentLimit = "800000",
            Places =
            [
                new AnalysisPlaceRequest
                {
                    Name = placeName,
                    Address = "Avenida Rivadavia 1234",
                    Latitude = -34.61,
                    Longitude = -58.43,
                    Type = "Trabajo",
                    WhoGoes = personNames.Count == 0 ? "Persona 1" : personNames[0],
                    Frequency = "diaria",
                    Priority = "alta",
                    MaximumTravelTime = "45 min"
                }
            ]
        }
    };
}
