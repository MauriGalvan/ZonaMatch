using NetTopologySuite.Geometries;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Infrastructure.Routing
{
    // adaptador provisorio hasta tener OSRM local (PBI 9): estima por distancia y modo
    public class EstimatedRoutingService : IRoutingService
    {
        public const string Method = "estimado";

        public Task<RouteEstimate> EstimateAsync(Point from, Point to, TravelMode mode, CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimate(TravelTimeEstimator.EstimateMinutes(from, to, mode), Method));
        }
    }
}
