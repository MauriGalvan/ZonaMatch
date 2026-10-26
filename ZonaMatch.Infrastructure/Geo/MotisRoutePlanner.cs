using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Infrastructure.Geo
{
    // Trip plans from MOTIS (the "motis" container of docker-compose, GET /api/v1/plan), with every leg and its
    // geometry. Like MotisRuteoClient, it asks for a weekday of the loaded reference week at 8 in the morning and
    // keeps each plan in memory for a day. Of the itineraries MOTIS offers, the best one is kept: the fastest, with
    // every transfer counted as TransferPenaltySeconds more and every public transport leg shorter than five blocks as
    // ShortRidePenaltySeconds more, so a bus for three blocks or an unnecessary transfer does not win by a minute.
    public class MotisRoutePlanner(HttpClient http, IMemoryCache cache, IConfiguration configuration) : IRoutePlanner
    {
        // Tuesday, March 3, 2026, 8:00 in Buenos Aires (UTC-3), as in MotisRuteoClient
        private const string DefaultReferenceTime = "2026-03-03T11:00:00Z";

        private static readonly TimeSpan PlanLifetime = TimeSpan.FromDays(1);

        private const int TransferPenaltySeconds = 300;

        // A ride of five blocks or less (ModosViaje.DistanciaACaminarKm) is better walked
        private const int ShortRidePenaltySeconds = 600;

        // Longest direct walk or bike ride searched (seconds)
        private const int MaxDirectSeconds = 7200;

        private readonly string _referenceTime = configuration["Motis:FechaReferencia"] ?? DefaultReferenceTime;

        public async Task<RoutePlan?> PlanAsync(
            double fromLatitude, double fromLongitude, double toLatitude, double toLongitude, ModoViaje mode,
            CancellationToken cancellationToken = default)
        {
            var key = string.Create(CultureInfo.InvariantCulture,
                $"motis-plan:{mode}:{fromLatitude:F4},{fromLongitude:F4}:{toLatitude:F4},{toLongitude:F4}");

            if (cache.TryGetValue(key, out RoutePlan? cached))
                return cached;

            var url = string.Create(CultureInfo.InvariantCulture,
                $"api/v1/plan?fromPlace={fromLatitude},{fromLongitude}&toPlace={toLatitude},{toLongitude}&time={_referenceTime}&{Modes(mode)}");

            Plan? plan;
            try
            {
                plan = await http.GetFromJsonAsync<Plan>(url, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new ServiceUnavailableException("The routing service (MOTIS) is not available.", ex);
            }

            // The best of the transit itineraries and the direct trip (walking can beat a bus)
            var best = (plan?.Itineraries ?? []).Concat(plan?.Direct ?? [])
                .Where(itinerary => itinerary.Legs is { Count: > 0 })
                .Select(itinerary => (itinerary.Duration, itinerary.Transfers, Legs: itinerary.Legs!.Select(ToLeg).ToList()))
                .MinBy(option => option.Duration
                                 + TransferPenaltySeconds * option.Transfers
                                 + ShortRidePenaltySeconds * option.Legs.Count(IsShortRide));

            var result = best.Legs is null ? null : new RoutePlan(best.Duration, best.Legs);
            cache.Set(key, result, PlanLifetime);
            return result;
        }

        // Public transport with walks; the other modes, only the direct trip
        private static string Modes(ModoViaje mode) => mode switch
        {
            ModoViaje.Auto => "transitModes=&directModes=CAR",
            ModoViaje.Bici => $"transitModes=&directModes=BIKE&maxDirectTime={MaxDirectSeconds}",
            ModoViaje.APie => $"transitModes=&directModes=WALK&maxDirectTime={MaxDirectSeconds}",
            _ => "transitModes=TRANSIT&directModes=WALK",
        };

        private static RouteLeg ToLeg(Leg leg)
        {
            var isTransit = leg.RouteShortName is not null || leg.DisplayName is not null;

            return new RouteLeg(
                ModeCode(leg.Mode),
                leg.Duration,
                leg.Distance,
                PlaceName(leg.From?.Name),
                PlaceName(leg.To?.Name),
                isTransit ? leg.DisplayName ?? leg.RouteShortName : null,
                isTransit ? leg.Headsign : null,
                isTransit ? leg.AgencyName : null,
                isTransit && !string.IsNullOrWhiteSpace(leg.RouteColor) ? leg.RouteColor : null,
                leg.Geometry is { Points: { } points } geometry ? Polyline.Decode(points, geometry.Precision ?? 6) : []);
        }

        private static readonly HashSet<string> WalkingModes = ["walk", "bike", "car"];

        // A public transport leg shorter than five blocks along its path
        private static bool IsShortRide(RouteLeg leg) =>
            !WalkingModes.Contains(leg.Mode) && leg.Path.Count > 1 && PathKm(leg.Path) <= ModosViaje.DistanciaACaminarKm;

        private static double PathKm(IReadOnlyList<RoutePoint> path) =>
            path.Zip(path.Skip(1), (a, b) => ModosViaje.DistanciaKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude)).Sum();

        // MOTIS names the ends of the trip START and END
        private static string? PlaceName(string? name) => name is null or "START" or "END" ? null : name;

        private static string ModeCode(string? mode) => mode switch
        {
            "WALK" => "walk",
            "BIKE" or "RENTAL" => "bike",
            "CAR" or "CAR_PARKING" => "car",
            "BUS" or "COACH" => "bus",
            "SUBWAY" or "METRO" => "subway",
            "TRAM" => "tram",
            "FERRY" => "ferry",
            "RAIL" or "REGIONAL_RAIL" or "REGIONAL_FAST_RAIL" or "HIGHSPEED_RAIL" or "LONG_DISTANCE" or "NIGHT_RAIL"
                or "SUBURBAN" => "train",
            _ => "bus",
        };

        private sealed class Plan
        {
            [JsonPropertyName("itineraries")]
            public List<Itinerary>? Itineraries { get; set; }

            [JsonPropertyName("direct")]
            public List<Itinerary>? Direct { get; set; }
        }

        private sealed class Itinerary
        {
            // Seconds
            [JsonPropertyName("duration")]
            public int Duration { get; set; }

            [JsonPropertyName("transfers")]
            public int Transfers { get; set; }

            [JsonPropertyName("legs")]
            public List<Leg>? Legs { get; set; }
        }

        private sealed class Leg
        {
            [JsonPropertyName("mode")]
            public string? Mode { get; set; }

            [JsonPropertyName("duration")]
            public int Duration { get; set; }

            // Meters, only for walking, cycling and driving legs
            [JsonPropertyName("distance")]
            public double? Distance { get; set; }

            [JsonPropertyName("from")]
            public Place? From { get; set; }

            [JsonPropertyName("to")]
            public Place? To { get; set; }

            [JsonPropertyName("displayName")]
            public string? DisplayName { get; set; }

            [JsonPropertyName("routeShortName")]
            public string? RouteShortName { get; set; }

            [JsonPropertyName("headsign")]
            public string? Headsign { get; set; }

            [JsonPropertyName("agencyName")]
            public string? AgencyName { get; set; }

            [JsonPropertyName("routeColor")]
            public string? RouteColor { get; set; }

            [JsonPropertyName("legGeometry")]
            public Geometry? Geometry { get; set; }
        }

        private sealed class Place
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class Geometry
        {
            // Google encoded polyline
            [JsonPropertyName("points")]
            public string? Points { get; set; }

            // Decimal digits of the encoded coordinates (MOTIS uses 7)
            [JsonPropertyName("precision")]
            public int? Precision { get; set; }
        }
    }
}
