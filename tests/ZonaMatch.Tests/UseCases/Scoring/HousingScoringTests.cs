using FluentAssertions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring
{
    public class HousingScoringTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData(0, null)]
        [InlineData(1, 1)]
        [InlineData(5, 5)]
        public void ValidateRooms_WhenOptionalOrInRange_ReturnsThem(int? requested, int? expected)
        {
            var rooms = HousingScoring.ValidateRooms(requested);

            rooms.Should().Be(expected);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void ValidateRooms_WhenOutOfRange_ThrowsBadRequestException(int rooms)
        {
            var validate = () => HousingScoring.ValidateRooms(rooms);

            validate.Should().Throw<BadRequestException>();
        }

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

        [Fact]
        public void Sale_WhenZoneHasCreditData_ReturnsTheCreditDistributionAsLoaded()
        {
            var zone = new ZoneProfile
            {
                Slug = "ramos-mejia", Name = "Ramos Mejía", AverageSalePriceUsd = 150_000, AvailablePropertiesForSale = 900,
                CreditSalesBySegment = new Dictionary<(string Type, int Rooms), CreditSales>
                {
                    [(HousingTypes.All, 0)] = new(2348, [60_000, 115_000, 300_000], [0.1m, 0.544m, 1m]),
                },
            };

            var sale = HousingScoring.Sale(zone, null);

            sale!.CreditDistribution.Should().Equal(60_000, 115_000, 300_000);
            sale.CreditDistributionShares.Should().Equal(0.1m, 0.544m, 1m);
            sale.CreditPropertyType.Should().Be(HousingTypes.All);
            sale.CreditRooms.Should().BeNull();
            sale.CreditListings.Should().Be(2348);
        }

        [Fact]
        public void Sale_WhenSearchingATypeAndSize_ReturnsTheCreditSalesOfThatSegment()
        {
            var zone = new ZoneProfile
            {
                Slug = "ramos-mejia", Name = "Ramos Mejía", AverageSalePriceUsd = 150_000, AvailablePropertiesForSale = 900,
                MarketByPropertyType = new Dictionary<string, HousingMarket>
                {
                    [HousingTypes.Apartment] = new(null, null, null, 120_000, 500),
                },
                CreditSalesBySegment = new Dictionary<(string Type, int Rooms), CreditSales>
                {
                    [(HousingTypes.All, 0)] = new(2348, [60_000, 115_000, 300_000], [0.1m, 0.544m, 1m]),
                    [(HousingTypes.Apartment, 2)] = new(578, [43_000, 95_000], [0.052m, 0.817m]),
                },
            };

            var sale = HousingScoring.Sale(zone, HousingTypes.Apartment, rooms: 2);

            sale!.CreditDistribution.Should().Equal(43_000, 95_000);
            sale.CreditPropertyType.Should().Be(HousingTypes.Apartment);
            sale.CreditRooms.Should().Be(2);
            sale.CreditListings.Should().Be(578);
        }

        [Fact]
        public void Sale_WhenZoneHasNoCreditData_LeavesTheCreditDistributionEmpty()
        {
            var zone = new ZoneProfile { Slug = "zona", Name = "Zona", AverageSalePriceUsd = 150_000, AvailablePropertiesForSale = 900 };

            var sale = HousingScoring.Sale(zone, null);

            sale!.CreditDistribution.Should().BeNull();
            sale.CreditDistributionShares.Should().BeNull();
        }
    }
}
