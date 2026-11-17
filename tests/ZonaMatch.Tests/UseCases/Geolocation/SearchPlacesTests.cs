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

        [Fact]
        public async Task ExecuteAsync_WhenNoVariantFindsPlaces_ReturnsAnEmptyListAfterTryingEveryVariant()
        {
            const string search = "Av Maipu 99999";
            var client = new FakePlaceSearchClient("no-match");

            var results = await new SearchPlaces(client).ExecuteAsync(search, CancellationToken.None);

            Assert.Empty(results);
            Assert.Equal(PlaceQueryVariants.Build(search).Select(variant => variant.Query), client.Queries);
        }

        [Fact]
        public void BuildForTolerantEngines_ExpandsAbbreviationsAndDropsFillerWords()
        {
            var variants = PlaceQueryVariants.BuildForTolerantEngines("Av. Gral Paz nro 1200");

            Assert.Equal([("avenida general paz 1200", false), ("avenida general paz", true)], variants);
        }

        [Fact]
        public void BuildForTolerantEngines_ExpandsKnownAcronymsAndKeepsTheTypedQueryAsFallback()
        {
            Assert.Equal(
                [("universidad nacional de lanús", false), ("unla", false)],
                PlaceQueryVariants.BuildForTolerantEngines("UNLA"));
            Assert.Equal(
                [("universidad de buenos aires medicina", false), ("uba medicina", false)],
                PlaceQueryVariants.BuildForTolerantEngines("uba medicina"));
        }

        [Fact]
        public void BuildForTolerantEngines_DoesNotExpandPartialAcronymsOrCommonWords()
        {
            Assert.Equal([("unl", false)], PlaceQueryVariants.BuildForTolerantEngines("unl"));
            Assert.Equal([("uno", false)], PlaceQueryVariants.BuildForTolerantEngines("uno"));
        }

        [Fact]
        public void BuildForTolerantEngines_KeepsTheWordBeingTypedAsAPrefix()
        {
            Assert.Equal([("hosp", false)], PlaceQueryVariants.BuildForTolerantEngines("hosp"));
            Assert.Equal([("hospital", false)], PlaceQueryVariants.BuildForTolerantEngines("hosp."));
            Assert.Equal([("hospital central", false)], PlaceQueryVariants.BuildForTolerantEngines("hosp central"));
        }

        [Fact]
        public void BuildForTolerantEngines_KeepsTheLocalityAndFallsBackToTheStreetWithoutNumber()
        {
            var variants = PlaceQueryVariants.BuildForTolerantEngines("Gutenberg 666, Villa Luzuriaga");

            Assert.Equal([("gutenberg 666, villa luzuriaga", false), ("gutenberg", true)], variants);
        }

        [Fact]
        public async Task ExecuteAsync_WithATolerantEngine_SendsTheCleanedQueryThenTheStreetWithoutNumber()
        {
            var client = new FakePlaceSearchClient("avenida maipu") { UsesVariants = false };

            var result = Assert.Single(await new SearchPlaces(client).ExecuteAsync("  Av Maipu nro 99999 ", CancellationToken.None));

            Assert.True(result.IsApproximate);
            Assert.Equal(["avenida maipu 99999", "avenida maipu"], client.Queries);
        }

        [Fact]
        public async Task ExecuteAsync_WithProvider_UsesTheClientTheSelectorReturns()
        {
            var defaultClient = new FakePlaceSearchClient("no-match");
            // Not an acronym from PlaceQueryVariants.Siglas, so Photon receives the query as cleaned (lowercase)
            var photonClient = new FakePlaceSearchClient("maipu") { UsesVariants = false };
            var selector = new FakeSelector(photonClient);

            var result = Assert.Single(await new SearchPlaces(defaultClient, selector).ExecuteAsync("Maipu", "photon", CancellationToken.None));

            Assert.False(result.IsApproximate);
            Assert.Equal("photon", selector.RequestedProvider);
            Assert.Empty(defaultClient.Queries);
            Assert.Single(photonClient.Queries);
        }

        [Fact]
        public async Task ExecuteAsync_WithoutProvider_UsesTheDefaultClient()
        {
            var defaultClient = new FakePlaceSearchClient("UNLaM");
            var selector = new FakeSelector(new FakePlaceSearchClient("no-match"));

            await new SearchPlaces(defaultClient, selector).ExecuteAsync("UNLaM", CancellationToken.None);

            Assert.Single(defaultClient.Queries);
            Assert.Null(selector.RequestedProvider);
        }

        private sealed class FakeSelector(IPlaceSearchClient client) : IPlaceSearchClientSelector
        {
            public string? RequestedProvider { get; private set; }

            public IPlaceSearchClient Resolve(string? provider)
            {
                RequestedProvider = provider;
                return client;
            }
        }

        private sealed class FakePlaceSearchClient(string knownQuery) : IPlaceSearchClient
        {
            public List<string> Queries { get; } = [];

            public bool UsesVariants { get; init; } = true;

            bool IPlaceSearchClient.UsesQueryVariants => UsesVariants;

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
