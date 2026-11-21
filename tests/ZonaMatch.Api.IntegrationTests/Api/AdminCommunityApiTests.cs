using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.DTOs.Contribution.Requests;
using ZonaMatch.Application.DTOs.Contribution.Responses;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

// Admin validation page: contributions and questions of the zone community.
[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminCommunityApiTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task ListContributions_AfterAnAdministratorApprovesOne_MovesItToTheHistory()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var contributor = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var zoneSlug = IntegrationZoneRepository.CreateContributionZoneSlug();

        var createResponse = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/contributions/corrections",
            new CreateContributionCorrectionRequest(
                PointOfInterestId: $"n{Random.Shared.NextInt64(1, 9_000_000_000_000_000_000).ToString(CultureInfo.InvariantCulture)}",
                PointOfInterestName: "Biblioteca vieja",
                Reason: CorrectionReason.Name,
                ProposedName: "Biblioteca nueva",
                ProposedCategory: null,
                Latitude: null,
                Longitude: null,
                Comment: "Cambió el nombre."),
            ApiJson.Options);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var contribution = (await createResponse.Content.ReadFromJsonAsync<ContributionResponse>(ApiJson.Options))!;

        var before = await administrator.Client.GetFromJsonAsync<AdminContributionListResponse>("/api/v1/admin/contributions");
        before!.Pending.Should().ContainSingle(item => item.Id == contribution.Id).Which.Type.Should().Be("correction");

        (await administrator.Client.PutAsJsonAsync(
            $"/api/v1/zones/{zoneSlug}/contributions/{contribution.Id}/resolution", new ResolveContributionRequest(Approve: true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await administrator.Client.GetFromJsonAsync<AdminContributionListResponse>("/api/v1/admin/contributions");
        after!.Pending.Should().NotContain(item => item.Id == contribution.Id);
        var resolved = after.History.Should().ContainSingle(item => item.Id == contribution.Id).Subject;
        resolved.Status.Should().Be("approved");
        resolved.ResolvedByEmail.Should().Be(administrator.User.Email);
        resolved.AuthorEmail.Should().Be(contributor.User.Email);
    }

    [Fact]
    public async Task ListQuestions_ReturnsTheQuestionWithItsAnswerAndDeleteRemovesIt()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var author = await TestApiUser.RegisterAsync(factory);
        using var neighbor = await TestApiUser.RegisterAsync(factory);
        using var administrator = await TestApiUser.RegisterAsync(factory);
        administrator.AuthenticateAsAdministrator(factory);
        var questionUrl = $"/api/v1/zones/{IntegrationZoneRepository.CreateCommunityZoneSlug()}/questions";

        var questionResponse = await author.Client.PostAsJsonAsync(questionUrl, new CreateQuestionRequest("¿Hay plazas cerca?"));
        questionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var question = (await questionResponse.Content.ReadFromJsonAsync<QuestionResponse>())!;
        (await neighbor.Client.PostAsJsonAsync($"{questionUrl}/{question.Id}/answers", new CreateAnswerRequest("Sí, a dos cuadras.")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var questions = await administrator.Client.GetFromJsonAsync<List<AdminQuestionResponse>>("/api/v1/admin/questions");
        var listed = questions.Should().ContainSingle(item => item.Id == question.Id).Subject;
        listed.AuthorEmail.Should().Be(author.User.Email);
        listed.Answers.Should().ContainSingle().Which.AuthorEmail.Should().Be(neighbor.User.Email);

        (await administrator.Client.DeleteAsync($"/api/v1/admin/questions/{question.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var zoneQuestions = await author.Client.GetFromJsonAsync<List<QuestionResponse>>(questionUrl);
        zoneQuestions.Should().NotContain(item => item.Id == question.Id);
        (await administrator.Client.DeleteAsync($"/api/v1/admin/questions/{question.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/v1/admin/contributions")]
    [InlineData("/api/v1/admin/questions")]
    public async Task List_WhenNotAdministrator_ReturnsForbidden(string path)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        (await member.Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteQuestion_WhenNotAdministrator_ReturnsForbidden()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var member = await TestApiUser.RegisterAsync(factory);

        (await member.Client.DeleteAsync($"/api/v1/admin/questions/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
