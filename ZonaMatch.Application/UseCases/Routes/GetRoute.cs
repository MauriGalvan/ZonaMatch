using ZonaMatch.Application.DTOs.Routes.Requests;
using ZonaMatch.Application.DTOs.Routes.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Routes
{
    // Fastest trip from a point (the center of a zone) to one of the user's places, with the legs to draw it:
    // in public transport, which line goes on each leg.
    public class GetRoute(IRoutePlanner planner)
    {
        public async Task<RouteResponse> ExecuteAsync(RouteRequest request, CancellationToken cancellationToken = default)
        {
            // Coordinates outside the world are a malformed request, not a missing route
            if (!IsValid(request.FromLatitude, request.FromLongitude) || !IsValid(request.ToLatitude, request.ToLongitude))
                throw new BadRequestException("The origin and the destination need a valid latitude and longitude.");

            var requestedMode = ModosViaje.DesdeCodigo(request.Mode)
                ?? throw new BadRequestException("Unknown travel mode: use transporte-publico, auto, bici or a-pie.");

            // Ten blocks or less are walked, as in the scoring: the response says which mode was used
            var mode = ModosViaje.ParaDistancia(
                requestedMode, request.FromLatitude, request.FromLongitude, request.ToLatitude, request.ToLongitude);

            var plan = await planner.PlanAsync(
                request.FromLatitude, request.FromLongitude, request.ToLatitude, request.ToLongitude, mode,
                cancellationToken);

            // The routing service answered but found no trip for that mode (too far to walk, no service...)
            if (plan is null || plan.Legs.Count == 0)
                throw new NotFoundException("No trip was found between those points for the selected travel mode.");

            return new RouteResponse(
                ModosViaje.Codigo(mode),
                Minutes(plan.DurationSeconds),
                plan.Legs.Select(leg => new RouteLegResponse(
                    leg.Mode,
                    Minutes(leg.DurationSeconds),
                    leg.DistanceMeters is { } meters ? (int)Math.Round(meters) : null,
                    leg.FromName,
                    leg.ToName,
                    leg.LineName,
                    leg.Headsign,
                    leg.AgencyName,
                    leg.Color,
                    leg.Path.Select(point => new[] { point.Latitude, point.Longitude }).ToList())).ToList());
        }

        private static bool IsValid(double latitude, double longitude) =>
            latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && !(latitude == 0 && longitude == 0);

        // A leg shorter than a minute still shows as one minute
        private static int Minutes(int seconds) => Math.Max(1, (int)Math.Round(seconds / 60.0));
    }
}
