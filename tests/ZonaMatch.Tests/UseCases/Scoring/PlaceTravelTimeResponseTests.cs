using FluentAssertions;
using System.Text.Json;
using ZonaMatch.Application.DTOs.Scoring.Responses;

namespace ZonaMatch.Tests.UseCases.Scoring;

public class PlaceTravelTimeResponseTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(8.75)]
    [InlineData(null)]
    public void Serialize_WhenDistanceIsAvailableOrMissing_UsesNumberOrNull(double? distanceKm)
    {
        var place = new PlaceTravelTimeResponse("Trabajo", 25, 45, false, distanceKm);
        var json = JsonSerializer.Serialize(place, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var field = document.RootElement.GetProperty("distanceKm");

        if (distanceKm is { } km)
            field.GetDouble().Should().Be(km);
        else
            field.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
