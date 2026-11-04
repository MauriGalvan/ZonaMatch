using FluentAssertions;
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

            isValid.Should().BeTrue();
            categories.Should().Equal(PointOfInterestCategory.All);
        }

        [Fact]
        public void TryParseList_WhenListHasCaseWhitespaceAndDuplicates_NormalizesCategories()
        {
            var isValid = PointOfInterestCategory.TryParseList(" Transporte, salud,transporte ", out var categories, out _);

            isValid.Should().BeTrue();
            categories.Should().Equal(PointOfInterestCategory.PublicTransport, PointOfInterestCategory.Healthcare);
        }

        [Fact]
        public void TryParseList_WhenCategoryIsUnknown_ReturnsItsName()
        {
            var isValid = PointOfInterestCategory.TryParseList("salud,boliches", out var categories, out var error);

            isValid.Should().BeFalse();
            categories.Should().BeEmpty();
            error.Should().Contain("boliches");
        }
    }
}
