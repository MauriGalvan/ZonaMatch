using ZonaMatch.Application.Common;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.Territory;
using ZonaMatch.Domain.Text;

namespace ZonaMatch.Application.Territory
{
    public class ZoneQueryService
    {
        public const int MaxSearchResults = 50;

        private static readonly string[] DetailIndicators =
        [
            IndicatorCodes.Population,
            IndicatorCodes.CrimeRatePer1000,
            IndicatorCodes.AverageRent,
            IndicatorCodes.InternetHouseholdsPct,
        ];

        private readonly IZoneRepository _zones;
        private readonly ITerritorialUnitRepository _units;
        private readonly IndicatorService _indicators;

        public ZoneQueryService(IZoneRepository zones, ITerritorialUnitRepository units, IndicatorService indicators)
        {
            _zones = zones;
            _units = units;
            _indicators = indicators;
        }

        public async Task<IReadOnlyList<ZoneSummaryDto>> SearchAsync(string? query, int limit, CancellationToken cancellationToken)
        {
            if (limit is < 1 or > MaxSearchResults)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, $"El límite debe estar entre 1 y {MaxSearchResults}.");
            }

            var zones = await _zones.SearchAsync(TextNormalizer.Normalize(query), limit, cancellationToken);
            return zones.Select(ZoneSummaryDto.From).ToList();
        }

        public async Task<ZoneDetailDto> GetDetailAsync(string slug, CancellationToken cancellationToken)
        {
            var zone = await GetZoneAsync(slug, cancellationToken);
            var indicators = await _indicators.ResolveAsync([zone], DetailIndicators, cancellationToken);

            return new ZoneDetailDto(
                zone.Slug,
                zone.Name,
                zone.ParentName,
                $"{zone.Name}, {zone.ParentName}",
                ZoneSummaryDto.TypeName(zone.Type),
                zone.ParentUnitName,
                Math.Round(zone.AreaM2 / 1_000_000d, 2),
                CoordinatesDto.From(zone.Center),
                zone.Geometry,
                indicators[zone.Id].Values.ToList());
        }

        public async Task<IReadOnlyList<ZoneSummaryDto>> GetNeighborsAsync(string slug, CancellationToken cancellationToken)
        {
            var zone = await GetZoneAsync(slug, cancellationToken);
            var neighbors = await _zones.GetNeighborsAsync(zone.Id, cancellationToken);
            return neighbors.Select(ZoneSummaryDto.From).ToList();
        }

        public async Task<TerritorialContextDto> GetContextAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            if (latitude is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "La latitud debe estar entre -90 y 90.");
            }

            if (longitude is < -180 or > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "La longitud debe estar entre -180 y 180.");
            }

            var point = GeometryNormalizer.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(longitude, latitude));

            var zoneUnit = await _units.FindContainingAsync(
                [TerritorialUnitType.Barrio, TerritorialUnitType.Localidad], point, cancellationToken);
            var department = await _units.FindContainingAsync(
                [TerritorialUnitType.Comuna, TerritorialUnitType.Partido], point, cancellationToken);
            var censusTract = await _units.FindContainingAsync(
                [TerritorialUnitType.CensusTract], point, cancellationToken);

            var zone = zoneUnit is null
                ? null
                : await _zones.GetByTerritorialUnitIdAsync(zoneUnit.Id, cancellationToken);

            return new TerritorialContextDto(
                CoordinatesDto.From(point),
                zone is null ? null : ZoneSummaryDto.From(zone),
                ToDto(zoneUnit),
                ToDto(department),
                ToDto(censusTract));
        }

        private async Task<ZoneData> GetZoneAsync(string slug, CancellationToken cancellationToken)
        {
            return await _zones.GetBySlugAsync(slug, cancellationToken)
                ?? throw new NotFoundException("la zona", slug);
        }

        private static TerritorialUnitDto? ToDto(TerritorialUnitSummary? unit) =>
            unit is null ? null : new TerritorialUnitDto(unit.Name, ZoneSummaryDto.TypeName(unit.Type), unit.Code);
    }
}
