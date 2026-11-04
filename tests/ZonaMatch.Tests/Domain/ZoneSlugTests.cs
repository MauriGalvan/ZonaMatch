using FluentAssertions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class ZoneSlugTests
    {
        [Theory]
        [InlineData("Villa Luro", "villa-luro")]
        [InlineData("Núñez", "nunez")]
        [InlineData("Villa Gral. Mitre", "villa-gral-mitre")]
        [InlineData("  San José (Almirante Brown) ", "san-jose-almirante-brown")]
        [InlineData("Comuna 10", "comuna-10")]
        public void Create_WhenNameHasAccentsOrPunctuation_ReturnsNormalizedSlug(string name, string expected)
        {
            ZoneSlug.Create(name).Should().Be(expected);
        }

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
