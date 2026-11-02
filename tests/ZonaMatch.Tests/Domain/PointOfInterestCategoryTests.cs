using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class PointOfInterestCategoryTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" , ")]
        public void TryParseList_WhenListIsEmpty_ReturnsAllCategories(string? list)
        {
            var isValid = PointOfInterestCategory.TryParseList(list, out var categories, out _);

            Assert.True(isValid);
            Assert.Equal(PointOfInterestCategory.All, categories);
        }

        [Fact]
        public void TryParseList_NormalizesCaseWhitespaceAndDuplicates()
        {
            var isValid = PointOfInterestCategory.TryParseList(" Transporte, salud,transporte ", out var categories, out _);

            Assert.True(isValid);
            Assert.Equal([PointOfInterestCategory.PublicTransport, PointOfInterestCategory.Healthcare], categories);
        }

        [Fact]
        public void TryParseList_WhenCategoryIsUnknown_ReturnsItsName()
        {
            var isValid = PointOfInterestCategory.TryParseList("salud,boliches", out var categories, out var error);

            Assert.False(isValid);
            Assert.Empty(categories);
            Assert.Contains("boliches", error);
        }
    }
}
