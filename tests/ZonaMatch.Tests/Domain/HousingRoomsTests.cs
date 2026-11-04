using FluentAssertions;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Tests.Domain
{
    public class HousingRoomsTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData(1, " de 1 ambiente")]
        [InlineData(3, " de 3 ambientes")]
        [InlineData(5, " de 5 ambientes o más")]
        public void TextSuffix_WhenRoomsChange_NamesTheRoomGroup(int? rooms, string expected)
        {
            var suffix = HousingRooms.TextSuffix(rooms);

            suffix.Should().Be(expected);
        }
    }
}
