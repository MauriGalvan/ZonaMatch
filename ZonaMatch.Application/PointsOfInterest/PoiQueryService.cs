using ZonaMatch.Application.Common;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Territory;
using ZonaMatch.Domain.PointsOfInterest;

namespace ZonaMatch.Application.PointsOfInterest
{
    public sealed record PoiCategoryDto(string Code, string Name, IReadOnlyList<PoiCategoryDto> Subcategories);

    public sealed record PoiDto(
        long Id,
        string Name,
        string? Address,
        string Category,
        string CategoryName,
        string Layer,
        string Ownership,
        CoordinatesDto Location,
        double DistanceMeters,
        string Source,
        DateOnly? SourceDate);

    public sealed record SubcategoryCountDto(string Code, string Name, int Count);

    public sealed record LayerCountDto(string Code, string Name, int Count, IReadOnlyList<SubcategoryCountDto> Subcategories);

    public sealed record ZonePoiSummaryDto(string Slug, int RadiusMeters, IReadOnlyList<LayerCountDto> Layers);

    public class PoiQueryService
    {
        public const int MinRadiusMeters = 100;
        public const int MaxRadiusMeters = 5000;
        public const int MaxResults = 500;

        private readonly IZoneRepository _zones;
        private readonly IPoiRepository _pois;
        private readonly IPoiCatalogRepository _catalog;

        public PoiQueryService(IZoneRepository zones, IPoiRepository pois, IPoiCatalogRepository catalog)
        {
            _zones = zones;
            _pois = pois;
            _catalog = catalog;
        }

        public static void ValidateRadius(int radiusMeters)
        {
            if (radiusMeters is < MinRadiusMeters or > MaxRadiusMeters)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(radiusMeters), radiusMeters, $"El radio debe estar entre {MinRadiusMeters} y {MaxRadiusMeters} metros.");
            }
        }

        public async Task<IReadOnlyList<PoiCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
        {
            var categories = await _catalog.GetCategoriesAsync(cancellationToken);

            return categories
                .Where(category => category.IsRoot)
                .OrderBy(category => category.SortOrder)
                .Select(root => new PoiCategoryDto(
                    root.Code,
                    root.Name,
                    categories
                        .Where(child => child.ParentId == root.Id)
                        .OrderBy(child => child.SortOrder)
                        .Select(child => new PoiCategoryDto(child.Code, child.Name, []))
                        .ToList()))
                .ToList();
        }

        public async Task<IReadOnlyList<PoiDto>> GetNearbyAsync(
            string slug,
            int radiusMeters,
            IReadOnlyCollection<string> layers,
            CancellationToken cancellationToken)
        {
            ValidateRadius(radiusMeters);
            var zone = await GetZoneAsync(slug, cancellationToken);
            var pois = await _pois.FindWithinAsync(zone.Center, radiusMeters, layers, MaxResults, cancellationToken);

            return pois
                .Select(poi => new PoiDto(
                    poi.Id,
                    poi.Name,
                    poi.Address,
                    poi.CategoryCode,
                    poi.CategoryName,
                    poi.RootCategoryCode,
                    OwnershipName(poi.Ownership),
                    CoordinatesDto.From(poi.Location),
                    Math.Round(poi.DistanceMeters),
                    poi.SourceName,
                    poi.SourceDate))
                .ToList();
        }

        // PBI 26a: conteos por capa y subcategoría dentro del radio
        public async Task<ZonePoiSummaryDto> GetSummaryAsync(string slug, int radiusMeters, CancellationToken cancellationToken)
        {
            ValidateRadius(radiusMeters);
            var zone = await GetZoneAsync(slug, cancellationToken);
            var counts = await _pois.CountWithinAsync(zone.Center, radiusMeters, cancellationToken);
            var categories = await _catalog.GetCategoriesAsync(cancellationToken);
            var countByCode = counts.ToDictionary(count => count.CategoryCode, count => count.Count);

            var layers = categories
                .Where(category => category.IsRoot)
                .OrderBy(category => category.SortOrder)
                .Select(root =>
                {
                    var subcategories = categories
                        .Where(child => child.ParentId == root.Id)
                        .OrderBy(child => child.SortOrder)
                        .Select(child => new SubcategoryCountDto(child.Code, child.Name, countByCode.GetValueOrDefault(child.Code)))
                        .ToList();

                    return new LayerCountDto(root.Code, root.Name, subcategories.Sum(child => child.Count), subcategories);
                })
                .ToList();

            return new ZonePoiSummaryDto(zone.Slug, radiusMeters, layers);
        }

        public static string OwnershipName(Ownership ownership) => ownership switch
        {
            Ownership.Public => "publica",
            Ownership.Private => "privada",
            _ => "desconocida",
        };

        private async Task<ZoneData> GetZoneAsync(string slug, CancellationToken cancellationToken)
        {
            return await _zones.GetBySlugAsync(slug, cancellationToken)
                ?? throw new NotFoundException("la zona", slug);
        }
    }
}
