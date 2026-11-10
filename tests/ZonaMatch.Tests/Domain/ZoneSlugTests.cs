using FluentAssertions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class ZoneSlugTests
    {
        [Theory]
        [InlineData("villa-luro", true)]
        [InlineData("comuna-10", true)]
        [InlineData("Villa-Luro", false)]
        [InlineData("villa--luro", false)]
        [InlineData("-villa", false)]
        [InlineData("villa luro", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValid_WhenSlugIsChecked_ReturnsExpectedValidity(string? slug, bool expected)
        {
            ZoneSlug.IsValid(slug).Should().Be(expected);
        }
    }
}
