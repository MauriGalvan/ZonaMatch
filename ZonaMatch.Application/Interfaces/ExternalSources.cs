using NetTopologySuite.Geometries;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Interfaces
{
    // registro tal como sale de una fuente, antes de normalizarlo
    public sealed record RawTerritorialFeature(string RawId, string? Name, string? Code, string? ParentCode, Geometry Geometry);

    public sealed record RawPoiFeature(
        string RawId,
        string? Name,
        string? Address,
        IReadOnlyDictionary<string, string> Attributes,
        Geometry Geometry);

    public interface ITerritorialSourceReader
    {
        IAsyncEnumerable<RawTerritorialFeature> ReadAsync(TerritorialSourceOptions source, CancellationToken cancellationToken);
    }

    public interface IPoiSourceReader
    {
        IAsyncEnumerable<RawPoiFeature> ReadAsync(PoiSourceOptions source, CancellationToken cancellationToken);
    }

    public sealed record RouteEstimate(double Minutes, string Method);

    // hoy estimación por distancia; mañana OSRM (PBI 9) sin tocar los casos de uso
    public interface IRoutingService
    {
        Task<RouteEstimate> EstimateAsync(Point from, Point to, TravelMode mode, CancellationToken cancellationToken);
    }
}
