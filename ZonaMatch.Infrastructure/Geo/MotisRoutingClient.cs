using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Infrastructure.Geo
{
    // Duration and distance for the same MOTIS itinerary (GET /api/v1/plan).
    // The loaded schedules use a reference week (etl/motis/config.yml): requests always use a weekday at 8 AM
    // from that week. Each trip is cached in memory for one day.
    // If MOTIS does not respond, requests are skipped for one minute to avoid repeated timeouts.
    public class MotisRoutingClient : IRoutingClient
    {
        // Tuesday, March 3, 2026, at 8:00 AM in Buenos Aires (UTC-3).
        private const string DefaultReferenceDate = "2026-03-03T11:00:00Z";

        private static readonly HybridCacheEntryOptions TravelTimeCacheDuration = new()
        {
            Expiration = TimeSpan.FromDays(1),
            LocalCacheExpiration = TimeSpan.FromDays(1),
        };
        private static readonly TimeSpan FailureBackoffDuration = TimeSpan.FromMinutes(1);

        // Maximum duration for direct walking or cycling routes, in seconds.
        private const int MaximumDirectRouteSeconds = 7200;

        private static DateTimeOffset _doNotQueryUntil = DateTimeOffset.MinValue;

        private readonly HttpClient _http;
        private readonly HybridCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MotisRoutingClient> _logger;
        private readonly string _referenceDate;

        public MotisRoutingClient(HttpClient http, HybridCache cache, TimeProvider timeProvider, IConfiguration configuration, ILogger<MotisRoutingClient> logger)
        {
            _http = http;
            _cache = cache;
            _timeProvider = timeProvider;
            _logger = logger;
            _referenceDate = configuration["Motis:FechaReferencia"] ?? DefaultReferenceDate;
        }

        public async Task<TravelTime?> GetTravelTimeAsync(
            double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude, TravelMode mode,
            CancellationToken cancellationToken = default)
        {
            // About 10 m: requests from the same center to the same destination share a result.
            var cacheKey = string.Create(CultureInfo.InvariantCulture,
                $"motis:{mode}:{originLatitude:F4},{originLongitude:F4}:{destinationLatitude:F4},{destinationLongitude:F4}");

            try
            {
                return await _cache.GetOrCreateAsync(cacheKey,
                    token => new ValueTask<TravelTime>(QueryAsync(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode, token)),
                    TravelTimeCacheDuration, cancellationToken: cancellationToken);
            }
            catch (TripNotAvailableException)
            {
                // MOTIS failed or is paused: the caller estimates the trip, and nothing is cached
                return null;
            }
        }

        // Only a trip MOTIS answered is returned (and cached); every failure throws TripNotAvailableException.
        private async Task<TravelTime> QueryAsync(
            double originLatitude, double originLongitude, double destinationLatitude, double destinationLongitude, TravelMode mode,
            CancellationToken cancellationToken)
        {
            if (_timeProvider.GetUtcNow() < _doNotQueryUntil)
                throw new TripNotAvailableException();

            try
            {
                var url = string.Create(CultureInfo.InvariantCulture,
                    $"api/v1/plan?fromPlace={originLatitude},{originLongitude}&toPlace={destinationLatitude},{destinationLongitude}&time={_referenceDate}&{Modes(mode)}&detailedLegs=true&detailedTransfers=true&useRoutedTransfers=true");

                var plan = await _http.GetFromJsonAsync<RoutingPlan>(url, cancellationToken);

                if (plan is null || plan.Error is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) })
                {
                    _logger.LogWarning("MOTIS could not calculate the trip: {Error}", plan?.Error);
                    throw new TripNotAvailableException();
                }

                // Select one itinerary first so its duration and distance are not mixed with another itinerary.
                var itinerary = (plan.Itineraries ?? [])
                    .Concat(plan.DirectRoutes ?? [])
                    .Where(item => item.DurationSeconds is >= 0)
                    .MinBy(item => item.DurationSeconds!.Value);
                return itinerary is null
                    ? new TravelTime(null, IsEstimated: false)
                    : new TravelTime(
                        (int)Math.Round(itinerary.DurationSeconds!.Value / 60.0),
                        IsEstimated: false,
                        DistanceKm: CalculateDistanceKm(itinerary));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "MOTIS returned an invalid response; route distance is unknown.");
                throw new TripNotAvailableException();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _doNotQueryUntil = _timeProvider.GetUtcNow() + FailureBackoffDuration;
                _logger.LogWarning("MOTIS is unavailable ({Message}); trips will be estimated from distance for one minute.", ex.Message);
                throw new TripNotAvailableException();
            }
        }

        // Signals a result that must not be cached; it never leaves this class.
        private sealed class TripNotAvailableException : Exception;

        private static double? CalculateDistanceKm(Itinerary itinerary)
        {
            if (itinerary.Segments is not { Count: > 0 } segments)
                return null;

            var totalMeters = 0.0;
            foreach (var segment in segments)
            {
                // For public transit, MOTIS may return geometry without the distance field.
                var distance = segment.DistanceMeters ?? RoutePolyline.GetLengthInMeters(
                    segment.Geometry?.Points, segment.Geometry?.Precision);

                // Do not report only the walking segments as the total trip distance.
                if (distance is not { } meters || !double.IsFinite(meters) || meters < 0)
                    return null;

                totalMeters += meters;
            }

            return double.IsFinite(totalMeters) ? totalMeters / 1000.0 : null;
        }

        // Public transit with walking transfers; other modes use direct routes only.
        // The walk to the first stop, from the last one and the walk-only trip can be as long as a direct walk: MOTIS
        // walks 15 minutes at most by default, so zones whose center is far from every stop got no trip at all even
        // with a train nearby. A long walk is chosen only when nothing faster exists.
        private static string Modes(TravelMode mode) => mode switch
        {
            TravelMode.Car => "transitModes=&directModes=CAR",
            // The whole trip by bike, or riding to and from a train, which carries bikes (REGIONAL_RAIL: the subway
            // is a separate mode and does not)
            TravelMode.Bicycle => $"transitModes=REGIONAL_RAIL&directModes=BIKE&preTransitModes=BIKE&postTransitModes=BIKE"
                                  + $"&maxDirectTime={MaximumDirectRouteSeconds}&maxPreTransitTime={MaximumDirectRouteSeconds}"
                                  + $"&maxPostTransitTime={MaximumDirectRouteSeconds}",
            TravelMode.Walking => $"transitModes=&directModes=WALK&maxDirectTime={MaximumDirectRouteSeconds}",
            _ => $"transitModes=TRANSIT&directModes=WALK&maxDirectTime={MaximumDirectRouteSeconds}"
                 + $"&maxPreTransitTime={MaximumDirectRouteSeconds}&maxPostTransitTime={MaximumDirectRouteSeconds}",
        };

        private sealed class RoutingPlan
        {
            [JsonPropertyName("itineraries")]
            public List<Itinerary>? Itineraries { get; set; }

            [JsonPropertyName("direct")]
            public List<Itinerary>? DirectRoutes { get; set; }

            [JsonPropertyName("error")]
            public JsonElement? Error { get; set; }
        }

        private sealed class Itinerary
        {
            // Duration in seconds.
            [JsonPropertyName("duration")]
            public int? DurationSeconds { get; set; }

            [JsonPropertyName("legs")]
            public List<RouteSegment>? Segments { get; set; }
        }

        private sealed class RouteSegment
        {
            [JsonPropertyName("distance")]
            public double? DistanceMeters { get; set; }

            [JsonPropertyName("legGeometry")]
            public RouteGeometry? Geometry { get; set; }
        }

        private sealed class RouteGeometry
        {
            [JsonPropertyName("points")]
            public string? Points { get; set; }

            [JsonPropertyName("precision")]
            public int? Precision { get; set; }
        }
    }
}
