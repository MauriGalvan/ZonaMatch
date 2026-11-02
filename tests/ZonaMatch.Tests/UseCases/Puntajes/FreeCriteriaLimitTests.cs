using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;

namespace ZonaMatch.Tests.UseCases.Puntajes
{
    public class FreeCriteriaLimitTests
    {
        private static readonly string[] Catalog = ["seguridad", "movilidad", "espacios-verdes", "educacion", "salud"];

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Validate_FreeWithUpToTwoCriteria_DoesNotThrow(int count)
        {
            FreeCriteriaLimit.Validate(true, Catalog.Take(count));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        public void Validate_FreeWithMoreThanTwoCriteria_ThrowsBadRequest(int count)
        {
            var error = Assert.Throws<BadRequestException>(() => FreeCriteriaLimit.Validate(true, Catalog.Take(count)));
            Assert.Contains("2 criterios", error.Message);
        }

        [Fact]
        public void Validate_PaidAccountWithEveryCriterion_DoesNotThrow()
        {
            FreeCriteriaLimit.Validate(false, Catalog);
        }

        [Fact]
        public void Validate_FreeWithRepeatedCriterion_CountsItOnce()
        {
            FreeCriteriaLimit.Validate(true, ["seguridad", "Seguridad", " seguridad ", "movilidad"]);
        }

        [Fact]
        public void Validate_FreeIgnoresBlankEntries()
        {
            FreeCriteriaLimit.Validate(true, ["seguridad", null, " ", "movilidad"]);
        }
    }
}