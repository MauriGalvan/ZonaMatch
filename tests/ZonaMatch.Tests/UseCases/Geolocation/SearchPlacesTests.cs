using ZonaMatch.Application.DTOs.Geolocation;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Geolocation;

namespace ZonaMatch.Tests.UseCases.Geolocation
{
    public class SearchPlacesTests
    {
        [Fact]
        public void Build_ExpandsAbbreviationsAndDropsFillerWords()
        {
            var queries = PlaceQueryVariants.Build("Av. Gral Paz nro 1200").Select(variant => variant.Query).ToList();

            Assert.Equal("Av. Gral Paz nro 1200", queries[0]);
            Assert.Contains("avenida general paz 1200", queries);
        }

        [Fact]
        public void Build_TriesTheStreetWithoutNumberLastAsApproximate()
        {
            var variants = PlaceQueryVariants.Build("maipu 4493, vicente lopez");

            Assert.Equal(("maipu 4493", false), variants[^2]);
            Assert.Equal(("maipu", true), variants[^1]);
        }

        [Fact]
        public void Build_WithoutNumber_HasNoApproximateVariant()
        {
            Assert.All(PlaceQueryVariants.Build("UNLaM"), variant => Assert.False(variant.IsApproximate));
        }

        [Fact]
        public async Task ExecuteAsync_FallsBackUntilAVariantFindsPlaces()
        {
            var client = new FakePlaceSearchClient("avenida maipu");
            var results = await new SearchPlaces(client).ExecuteAsync("Av Maipu 99999", CancellationToken.None);

            var result = Assert.Single(results);
            Assert.True(result.IsApproximate);
            Assert.Equal(["Av Maipu 99999", "avenida maipu 99999", "avenida maipu"], client.Queries);
        }

        [Fact]
        public async Task ExecuteAsync_StopsAtTheFirstQueryWithResults()
        {
            var client = new FakePlaceSearchClient("UNLaM");
            var result = Assert.Single(await new SearchPlaces(client).ExecuteAsync("UNLaM", CancellationToken.None));

            Assert.False(result.IsApproximate);
            Assert.Single(client.Queries);
        }

        private sealed class FakePlaceSearchClient(string knownQuery) : IPlaceSearchClient
        {
            public List<string> Queries { get; } = [];

            public Task<IReadOnlyList<PlaceSearchResult>> SearchAsync(string search, int limit, CancellationToken cancellationToken = default)
            {
                Queries.Add(search);
                IReadOnlyList<PlaceSearchResult> results = search == knownQuery
                    ? [new PlaceSearchResult { Id = "1", Name = "Lugar", Kind = "Calle", Latitude = -34.6, Longitude = -58.5 }]
                    : [];
                return Task.FromResult(results);
            }
        }
    }
}
