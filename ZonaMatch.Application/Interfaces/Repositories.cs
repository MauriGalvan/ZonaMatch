using NetTopologySuite.Geometries;
using ZonaMatch.Domain.PointsOfInterest;
using ZonaMatch.Domain.Sources;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Interfaces
{
    // proyecciones livianas que devuelven los repositorios para no cargar entidades completas

    public sealed record TerritorialUnitSummary(long Id, TerritorialUnitType Type, string? Code, string Name, long? ParentId);

    public sealed record ZoneSummary(int Id, string Slug, string Name, string ParentName, TerritorialUnitType Type);

    public sealed record ZoneData(
        int Id,
        string Slug,
        string Name,
        string ParentName,
        TerritorialUnitType Type,
        long TerritorialUnitId,
        long? ParentUnitId,
        string? ParentUnitName,
        double AreaM2,
        Point Center,
        MultiPolygon Geometry);

    public sealed record PoiData(
        long Id,
        string Name,
        string? Address,
        string CategoryCode,
        string CategoryName,
        string RootCategoryCode,
        Ownership Ownership,
        Point Location,
        string SourceName,
        DateOnly? SourceDate,
        double DistanceMeters);

    public sealed record PoiCategoryCount(string RootCategoryCode, string CategoryCode, int Count);

    public sealed record IndicatorData(
        long TerritorialUnitId,
        string IndicatorCode,
        decimal Value,
        string Unit,
        string SourceName,
        DateOnly ReferenceDate);

    public sealed record PoiDuplicateCandidate(PoiMatchCandidate First, PoiMatchCandidate Second, double DistanceMeters);

    public interface IDataSourceRepository
    {
        Task<DataSource?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    }

    public interface ITerritorialUnitRepository
    {
        // código oficial -> id, para resolver padres por atributo
        Task<IReadOnlyDictionary<string, long>> GetCodeIndexAsync(TerritorialUnitType type, CancellationToken cancellationToken);

        Task<TerritorialUnitSummary?> FindContainingAsync(
            IReadOnlyCollection<TerritorialUnitType> types,
            Point point,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<TerritorialUnitSummary>> GetByTypesAsync(
            IReadOnlyCollection<TerritorialUnitType> types,
            CancellationToken cancellationToken);

        // inserta o actualiza por (fuente, id externo)
        Task UpsertAsync(short dataSourceId, IReadOnlyList<TerritorialUnit> units, CancellationToken cancellationToken);
    }

    public interface IZoneRepository
    {
        // alinea la tabla de zonas con la lista recibida (inserta, actualiza y borra)
        Task SyncAsync(IReadOnlyList<Zone> zones, CancellationToken cancellationToken);

        Task<int> RebuildAdjacenciesAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<ZoneSummary>> SearchAsync(string normalizedQuery, int limit, CancellationToken cancellationToken);

        Task<ZoneData?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

        Task<IReadOnlyList<ZoneData>> GetBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken);

        Task<ZoneSummary?> GetByTerritorialUnitIdAsync(long territorialUnitId, CancellationToken cancellationToken);

        Task<IReadOnlyList<ZoneSummary>> GetNeighborsAsync(int zoneId, CancellationToken cancellationToken);
    }

    public interface IPoiCatalogRepository
    {
        Task<IReadOnlyList<PoiCategory>> GetCategoriesAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<PoiMappingRule>> GetRulesAsync(CancellationToken cancellationToken);
    }

    public interface IPoiRepository
    {
        Task UpsertAsync(short dataSourceId, IReadOnlyList<PointOfInterest> pois, CancellationToken cancellationToken);

        // excluye los POIs marcados como duplicados de otro
        Task<IReadOnlyList<PoiData>> FindWithinAsync(
            Point center,
            double radiusMeters,
            IReadOnlyCollection<string> rootCategoryCodes,
            int limit,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PoiCategoryCount>> CountWithinAsync(Point center, double radiusMeters, CancellationToken cancellationToken);

        // pares de POIs de fuentes distintas y misma categoría raíz, todavía no vinculados
        Task<IReadOnlyList<PoiDuplicateCandidate>> FindDuplicateCandidatesAsync(double maxDistanceMeters, CancellationToken cancellationToken);

        Task MarkDuplicatesAsync(IReadOnlyList<PoiDuplicate> duplicates, CancellationToken cancellationToken);
    }

    public interface IIndicatorRepository
    {
        Task<IReadOnlyList<IndicatorData>> GetAsync(
            IReadOnlyCollection<long> territorialUnitIds,
            IReadOnlyCollection<string> indicatorCodes,
            CancellationToken cancellationToken);
    }
}
