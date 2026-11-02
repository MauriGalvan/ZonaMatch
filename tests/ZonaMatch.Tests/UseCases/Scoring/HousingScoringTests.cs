using FluentAssertions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring
{
    public class HousingScoringTests
    {
        [Fact]
        public void Validate_WhenOperationIsMissing_DefaultsToRentAndIgnoresPurchaseLimit()
        {
            var (operation, purchaseLimit, housingType) = HousingScoring.Validate(null, 150_000, HousingTypes.All);

            operation.Should().Be(HousingOperations.Rent);
            purchaseLimit.Should().BeNull();
            housingType.Should().BeNull();
        }

        [Fact]
        public void Validate_WhenBuyingWithHousingType_ReturnsPurchaseLimitAndType()
        {
            var (operation, purchaseLimit, housingType) = HousingScoring.Validate("compra", 150_000, "ph");

            operation.Should().Be(HousingOperations.Purchase);
            purchaseLimit.Should().Be(150_000);
            housingType.Should().Be(HousingTypes.Ph);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(0, null)]
        [InlineData(34, 34)]
        public void ValidateSurface_WhenMissingOrZero_ReturnsNull(int? requested, int? expected)
        {
            HousingScoring.ValidateSurface(requested).Should().Be(expected);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(2001)]
        public void ValidateSurface_WhenOutsideSupportedRange_ThrowsBadRequestException(int surfaceM2)
        {
            Action act = () => HousingScoring.ValidateSurface(surfaceM2);

            act.Should().Throw<BadRequestException>();
        }

        [Theory]
        [InlineData("permuta", null, null)]
        [InlineData("compra", -1, null)]
        [InlineData("compra", 100, "loft")]
        public void Validate_WhenOperationPurchaseLimitOrHousingTypeIsInvalid_ThrowsBadRequestException(string operation, int? purchaseLimit, string? housingType)
        {
            Action act = () => HousingScoring.Validate(operation, purchaseLimit, housingType);

            act.Should().Throw<BadRequestException>();
        }
    }
}
