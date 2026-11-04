using FluentAssertions;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class TimeWindowTests
    {
        [Fact]
        public void All_WhenRead_CoversTheDayWithoutGaps()
        {
            var windows = TimeWindow.All;

            windows[0].StartHour.Should().Be(0);
            windows[^1].EndHour.Should().Be(24);
            for (var index = 1; index < windows.Count; index++)
                windows[index].StartHour.Should().Be(windows[index - 1].EndHour);
        }

        [Fact]
        public void DeparturesPerHour_WhenTripsExist_ReturnsExpectedFrequency()
        {
            var morning = TimeWindow.Morning; // Three hours.

            morning.DeparturesPerHour(35).Should().Be(11.7);
            morning.MinutesBetweenDepartures(35).Should().Be(5);
        }

        [Fact]
        public void DeparturesPerHour_WhenThereAreNoDepartures_ReturnsZeroAndNoInterval()
        {
            TimeWindow.Evening.DeparturesPerHour(0).Should().Be(0);
            TimeWindow.Evening.MinutesBetweenDepartures(0).Should().BeNull();
        }

        [Fact]
        public void FromCode_WhenCodeIsKnownOrUnknown_ReturnsWindowOrNull()
        {
            TimeWindow.FromCode("tarde").Should().BeSameAs(TimeWindow.Afternoon);
            TimeWindow.FromCode("siesta").Should().BeNull();
        }
    }
}
