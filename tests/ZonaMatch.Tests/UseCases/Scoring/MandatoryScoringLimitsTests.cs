using FluentAssertions;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Scoring;

namespace ZonaMatch.Tests.UseCases.Scoring
{
    public class MandatoryScoringLimitsTests
    {
        [Fact]
        public void Validate_WhenAllRequiredLimitsAreProvided_DoesNotThrow()
        {
            Action act = () =>
            {
                MandatoryScoringLimits.Validate("$ 600.000", "alquiler", null, ["45 min", "30"]);
                MandatoryScoringLimits.Validate("80000", "compra", 120_000, ["45 min"]);
            };

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("$ 0")]
        public void Validate_WhenMonthlyBudgetIsMissing_ThrowsBadRequestException(string? monthlyBudget)
        {
            Action act = () => MandatoryScoringLimits.Validate(monthlyBudget, "alquiler", null, ["45 min"]);

            act.Should().Throw<BadRequestException>().Which.Message.Should().Contain("gasto");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("0")]
        public void Validate_WhenBuying_ExpensesLimitIsOptional(string? expensesLimit)
        {
            Action act = () => MandatoryScoringLimits.Validate(expensesLimit, "compra", 120_000, ["45 min"]);

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_WhenBuyingWithoutPurchasePrice_ThrowsBadRequestException()
        {
            Action act = () => MandatoryScoringLimits.Validate("80000", "compra", null, ["45 min"]);

            act.Should().Throw<BadRequestException>().Which.Message.Should().Contain("compra");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("min")]
        public void Validate_WhenPlaceHasNoTravelTime_ThrowsBadRequestException(string? travelTime)
        {
            Action act = () => MandatoryScoringLimits.Validate("600000", "alquiler", null, ["45 min", travelTime]);

            act.Should().Throw<BadRequestException>().Which.Message.Should().Contain("tiempo");
        }
    }
}
