using NetTopologySuite.Geometries;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Territory
{
    public sealed record CoordinatesDto(double Latitude, double Longitude)
    {
        public static CoordinatesDto From(Point point) => new(point.Y, point.X);
    }

    public sealed record ZoneSummaryDto(string Slug, string Name, string ParentName, string DisplayName, string Type)
    {
        public static ZoneSummaryDto From(Interfaces.ZoneSummary zone) =>
            new(zone.Slug, zone.Name, zone.ParentName, $"{zone.Name}, {zone.ParentName}", TypeName(zone.Type));

        public static string TypeName(TerritorialUnitType type) => type switch
        {
            TerritorialUnitType.Barrio => "barrio",
            TerritorialUnitType.Localidad => "localidad",
            TerritorialUnitType.Comuna => "comuna",
            TerritorialUnitType.Partido => "partido",
            TerritorialUnitType.CensusTract => "radio_censal",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de unidad desconocido."),
        };
    }

    // valor de un indicador con su trazabilidad: fuente, fecha y si viene de la unidad superior
    public sealed record IndicatorValueDto(
        string Code,
        decimal Value,
        string Unit,
        string Source,
        DateOnly ReferenceDate,
        bool Inherited,
        string Level);

    public sealed record ZoneDetailDto(
        string Slug,
        string Name,
        string ParentName,
        string DisplayName,
        string Type,
        string? ContainerName,
        double AreaKm2,
        CoordinatesDto Center,
        Geometry Geometry,
        IReadOnlyList<IndicatorValueDto> Indicators);

    public sealed record TerritorialUnitDto(string Name, string Type, string? Code);

    // PBI 14: dónde cae un punto
    public sealed record TerritorialContextDto(
        CoordinatesDto Point,
        ZoneSummaryDto? Zone,
        TerritorialUnitDto? ZoneUnit,
        TerritorialUnitDto? Department,
        TerritorialUnitDto? CensusTract);
}
