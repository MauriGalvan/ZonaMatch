using FluentAssertions;

namespace ZonaMatch.Tests
{
    // Smoke test to ensure the Development CI pipeline has a test to run.
    public class DummyTests
    {
        [Fact]
        public void TestPipeline_WhenSmokeTestRuns_CompletesSuccessfully()
        {
            true.Should().BeTrue();
        }
    }
}
