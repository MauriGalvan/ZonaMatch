using System.Data.Common;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Group.Requests;
using ZonaMatch.Application.DTOs.Group.Responses;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ParticipantPersistenceTests(PostgreSqlDatabaseFixture database)
{
    [Fact]
    public async Task SaveChanges_WhenUserIsAddedTwiceToTheSameGroup_RejectsTheRepeatedParticipant()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var owner = await TestApiUser.RegisterAsync(factory);
        var createGroupResponse = await owner.Client.PostAsJsonAsync("/api/v1/groups", new CreateGroupRequest { Name = "Unique group" });
        var group = await createGroupResponse.Content.ReadFromJsonAsync<GroupResponse>();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ZonaMatchDbContext>();
        var ownerParticipant = await context.Set<Participant>().AsNoTracking().SingleAsync(participant => participant.GroupId == group!.Id);
        context.Set<Participant>().Add(new Participant { GroupId = ownerParticipant.GroupId, UserId = ownerParticipant.UserId, Role = ownerParticipant.Role });

        var save = () => context.SaveChangesAsync();

        (await save.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeAssignableTo<DbException>()
            .Which.SqlState.Should().Be("23505");
    }
}
