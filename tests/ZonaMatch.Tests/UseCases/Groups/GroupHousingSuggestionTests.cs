using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Groups;

public class GroupHousingSuggestionTests
{
    private static Context Profile(string operation, string? monthly, decimal? purchase = null, string? type = null, int? surface = null) =>
        new() { Operation = operation, MaximumRent = monthly, MaximumPurchasePriceUsd = purchase, HousingType = type, AreaM2 = surface };

    [Fact]
    public void The_group_looks_for_the_largest_surface()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Rent, "500000", surface: 34),
            Profile(HousingOperations.Rent, "400000", surface: 50),
            Profile(HousingOperations.Rent, "300000"),
        ]);

        Assert.Equal(50, suggestion.SurfaceM2);
    }

    [Fact]
    public void When_everyone_buys_the_group_buys_and_adds_up_the_caps()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Purchase, "150000", 100_000, HousingTypes.Apartment),
            Profile(HousingOperations.Purchase, "50000", 60_000, HousingTypes.Apartment),
        ]);

        Assert.Equal(new GroupHousingSuggestion(HousingOperations.Purchase, 200_000, 160_000, HousingTypes.Apartment), suggestion);
    }

    [Fact]
    public void A_member_that_rents_makes_the_group_rent_and_only_rents_add_up()
    {
        var suggestion = GroupHousingSuggestion.From([
            Profile(HousingOperations.Purchase, "150000", 100_000, HousingTypes.House),
            Profile(HousingOperations.Rent, "800000", type: HousingTypes.Ph),
        ]);

        // The 150.000 of the buyer are expenses, not rent: they do not count for a rent budget
        Assert.Equal(new GroupHousingSuggestion(HousingOperations.Rent, 800_000, null, null), suggestion);
    }
}
