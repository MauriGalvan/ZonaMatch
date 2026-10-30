using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;

namespace ZonaMatch.Tests.UseCases.Puntajes
{
    public class LimitesObligatoriosTests
    {
        [Fact]
        public void Validar_WithEveryLimit_DoesNotThrow()
        {
            LimitesObligatorios.Validar("$ 600.000", "alquiler", null, ["45 min", "30"]);
            LimitesObligatorios.Validar("80000", "compra", 120_000, ["45 min"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("$ 0")]
        public void Validar_WithoutMonthlyBudget_ThrowsBadRequest(string? gasto)
        {
            var error = Assert.Throws<BadRequestException>(() => LimitesObligatorios.Validar(gasto, "alquiler", null, ["45 min"]));
            Assert.Contains("gasto", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("0")]
        public void Validar_WhenBuying_ExpensasCapIsOptional(string? expensas)
        {
            LimitesObligatorios.Validar(expensas, "compra", 120_000, ["45 min"]);
        }

        [Fact]
        public void Validar_WhenBuyingWithoutPurchasePrice_ThrowsBadRequest()
        {
            var error = Assert.Throws<BadRequestException>(() => LimitesObligatorios.Validar("80000", "compra", null, ["45 min"]));
            Assert.Contains("compra", error.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("min")]
        public void Validar_WithoutTravelTime_ThrowsBadRequest(string? tiempo)
        {
            var error = Assert.Throws<BadRequestException>(() => LimitesObligatorios.Validar("600000", "alquiler", null, ["45 min", tiempo]));
            Assert.Contains("tiempo", error.Message);
        }
    }
}
