using FluentAssertions;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Group;

public class GroupHousingSuggestionTests
{
    private static Context Profile(
        string operation, string? monthly, decimal? purchase = null, string? type = null, int? surface = null, int? rooms = null) =>
        new()
        {
            Operation = operation, MaximumRent = monthly, MaximumPurchasePriceUsd = purchase, HousingType = type, AreaM2 = surface,
            Rooms = rooms,
        };

    [Fact]
    public void From_WhenMembersAskForRooms_SuggestsTheLargestNumber()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Rent, "500000", rooms: 2),
            Profile(HousingOperations.Rent, "400000", rooms: 3),
            Profile(HousingOperations.Rent, "300000"),
        ]);

        Assert.Equal(3, suggestion.Rooms);
    }

    [Fact]
    public void From_WhenProfilesHaveDifferentSurfaces_ReturnsLargestSurface()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Rent, "500000", surface: 34),
            Profile(HousingOperations.Rent, "400000", surface: 50),
            Profile(HousingOperations.Rent, "300000"),
        ]);

        suggestion.SurfaceM2.Should().Be(50);
    }

    [Fact]
    public void From_WhenEveryoneBuys_SumsPurchaseLimits()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Purchase, "150000", 100_000, HousingTypes.Apartment),
            Profile(HousingOperations.Purchase, "50000", 60_000, HousingTypes.Apartment),
        ]);

        suggestion.Should().Be(new GroupHousingSuggestion(HousingOperations.Purchase, 200_000, 160_000, HousingTypes.Apartment));
    }

    [Fact]
    public void From_WhenSomeMembersRent_UsesOnlyTheRentBudget()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Purchase, "150000", 100_000, HousingTypes.House),
            Profile(HousingOperations.Rent, "800000", type: HousingTypes.Ph),
        ]);

        // The 150.000 of the buyer are expenses, not rent: they do not count for a rent budget
        suggestion.Should().Be(new GroupHousingSuggestion(HousingOperations.Rent, 800_000, null, null));
    }
}
