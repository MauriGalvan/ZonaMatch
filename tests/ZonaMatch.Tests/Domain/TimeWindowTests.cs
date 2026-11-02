using ZonaMatch.Domain.Common;

namespace ZonaMatch.Tests.Domain
{
    public class TimeWindowTests
    {
        [Fact]
        public void All_CoversTheDayWithoutGaps()
        {
            var windows = TimeWindow.All;

            Assert.Equal(0, windows[0].StartHour);
            Assert.Equal(24, windows[^1].EndHour);
            for (var index = 1; index < windows.Count; index++)
                Assert.Equal(windows[index - 1].EndHour, windows[index].StartHour);
        }

        [Fact]
        public void FrequencyMethods_ReturnExpectedValues()
        {
            var morning = TimeWindow.Morning; // Three hours.

            Assert.Equal(11.7, morning.DeparturesPerHour(35));
            Assert.Equal(5, morning.MinutesBetweenDepartures(35));
        }

        [Fact]
        public void FrequencyMethods_WhenThereAreNoDepartures_ReturnNoFrequency()
        {
            Assert.Equal(0, TimeWindow.Evening.DeparturesPerHour(0));
            Assert.Null(TimeWindow.Evening.MinutesBetweenDepartures(0));
        }

        [Fact]
        public void FromCode_ReturnsMatchingWindow()
        {
            Assert.Same(TimeWindow.Afternoon, TimeWindow.FromCode("tarde"));
            Assert.Null(TimeWindow.FromCode("siesta"));
        }
    }
}
