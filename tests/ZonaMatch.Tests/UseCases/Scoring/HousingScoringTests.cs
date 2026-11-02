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

            Assert.Equal(HousingOperations.Rent, operation);
            Assert.Null(purchaseLimit);
            Assert.Null(housingType);
        }

        [Fact]
        public void Validate_WhenBuyingWithHousingType_ReturnsPurchaseLimitAndType()
        {
            var (operation, purchaseLimit, housingType) = HousingScoring.Validate("compra", 150_000, "ph");

            Assert.Equal(HousingOperations.Purchase, operation);
            Assert.Equal(150_000, purchaseLimit);
            Assert.Equal(HousingTypes.Ph, housingType);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(0, null)]
        [InlineData(34, 34)]
        public void ValidateSurface_WhenMissingOrZero_ReturnsNull(int? requested, int? expected)
        {
            Assert.Equal(expected, HousingScoring.ValidateSurface(requested));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(2001)]
        public void ValidateSurface_WhenOutsideSupportedRange_ThrowsBadRequestException(int surfaceM2)
        {
            Assert.Throws<BadRequestException>(() => HousingScoring.ValidateSurface(surfaceM2));
        }

        [Theory]
        [InlineData("permuta", null, null)]
        [InlineData("compra", -1, null)]
        [InlineData("compra", 100, "loft")]
        public void Validate_WhenOperationPurchaseLimitOrHousingTypeIsInvalid_ThrowsBadRequestException(string operation, int? purchaseLimit, string? housingType)
        {
            Assert.Throws<BadRequestException>(() => HousingScoring.Validate(operation, purchaseLimit, housingType));
        }
    }
}
