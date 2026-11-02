using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring
{
    public class MandatoryScoringLimitsTests
    {
        [Fact]
        public void Validate_WhenAllRequiredLimitsAreProvided_DoesNotThrow()
        {
            MandatoryScoringLimits.Validate("$ 600.000", "alquiler", null, ["45 min", "30"]);
            MandatoryScoringLimits.Validate("80000", "compra", 120_000, ["45 min"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("$ 0")]
        public void Validate_WhenMonthlyBudgetIsMissing_ThrowsBadRequestException(string? monthlyBudget)
        {
            var error = Assert.Throws<BadRequestException>(() => MandatoryScoringLimits.Validate(monthlyBudget, "alquiler", null, ["45 min"]));
            Assert.Contains("gasto", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("0")]
        public void Validate_WhenBuying_ExpensesLimitIsOptional(string? expensesLimit)
        {
            MandatoryScoringLimits.Validate(expensesLimit, "compra", 120_000, ["45 min"]);
        }

        [Fact]
        public void Validate_WhenBuyingWithoutPurchasePrice_ThrowsBadRequestException()
        {
            var error = Assert.Throws<BadRequestException>(() => MandatoryScoringLimits.Validate("80000", "compra", null, ["45 min"]));
            Assert.Contains("compra", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("min")]
        public void Validate_WhenPlaceHasNoTravelTime_ThrowsBadRequestException(string? travelTime)
        {
            var error = Assert.Throws<BadRequestException>(() => MandatoryScoringLimits.Validate("600000", "alquiler", null, ["45 min", travelTime]));
            Assert.Contains("tiempo", error.Message);
        }
    }
}
