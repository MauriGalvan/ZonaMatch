using FluentAssertions;
using Moq;
using ZonaMatch.Application.DTOs.ZoneRecommendation;
using ZonaMatch.Application.DTOs.ZoneRecommendation.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.ZoneRecommendation;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.UseCases.ZoneRecommendation;

public class GetZoneProfileRecommendationsTests
{
    [Fact]
    public async Task GetZoneProfileRecommendations_ResolvesRequestedProfileCategoriesAndOrigin()
    {
        var repository = new Mock<IPointOfInterestRepository>();
        (double Latitude, double Longitude)? expectedOrigin = (-34.6, -58.4);
        repository.Setup(item => item.GetProfileRecommendationsInZoneAsync(
                "villa-luro",
                It.Is<IReadOnlyCollection<string>>(categories => categories.Count == 7 &&
                    categories.Contains(ProfileRecommendationCategories.MaternalSchool) &&
                    categories.Contains(ProfileRecommendationCategories.Kindergarten) &&
                    categories.Contains(ProfileRecommendationCategories.PrimarySchool) &&
                    categories.Contains(ProfileRecommendationCategories.SecondarySchool) &&
                    categories.Contains(ProfileRecommendationCategories.Veterinary) &&
                    categories.Contains(ProfileRecommendationCategories.CarRepair) &&
                    categories.Contains(ProfileRecommendationCategories.BicycleShop)),
                100,
                expectedOrigin,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProfileRecommendationCandidate
                {
                    Id = "school-1", Name = "Maternal School", Category = ProfileRecommendationCategories.MaternalSchool,
                    Latitude = -34.61, Longitude = -58.41, DistanceMeters = 200, IsTruncated = true
                }
            });

        var result = await new GetZoneProfileRecommendations(repository.Object).ExecuteAsync("villa-luro", new()
        {
            AgeRanges = "0-2, 3-5, 6-11, 12-17",
            HasPets = true,
            HasVehicle = true,
            HasBicycle = true,
            Latitude = -34.6,
            Longitude = -58.4
        });

        var recommendation = result.Places.Should().ContainSingle().Which;
        recommendation.Name.Should().Be("Maternal School");
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task GetZoneProfileRecommendations_WhenNoRelevantProfileOptionsExist_ReturnsEmptyWithoutQueryingRepository()
    {
        var repository = new Mock<IPointOfInterestRepository>();

        var result = await new GetZoneProfileRecommendations(repository.Object)
            .ExecuteAsync("villa-luro", new GetZoneProfileRecommendationsRequest { AgeRanges = "unknown" });

        result.Places.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
        repository.Verify(item => item.GetProfileRecommendationsInZoneAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<int>(),
            It.IsAny<(double Latitude, double Longitude)?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
