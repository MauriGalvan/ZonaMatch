using ZonaMatch.Domain.Scoring;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Analysis
{
    public enum CriterionSourceKind
    {
        // cantidad de POIs de una capa dentro del radio
        PoiLayer,
        // indicador territorial (con herencia de comuna/partido)
        Indicator,
        // tiempo a los puntos importantes; sin puntos, oferta de transporte en el radio
        Mobility,
    }

    public sealed record CriterionDefinition(
        string Code,
        string Name,
        CriterionSourceKind Kind,
        string Reference,
        CriterionDirection Direction);

    // criterios que ofrecen las pantallas H02/H10 y de dónde sale el dato de cada uno
    public static class CriterionCatalog
    {
        public const string TransportLayer = "transporte";

        public static readonly IReadOnlyList<CriterionDefinition> All =
        [
            new("movilidad", "Movilidad", CriterionSourceKind.Mobility, TransportLayer, CriterionDirection.HigherIsBetter),
            new("seguridad", "Seguridad", CriterionSourceKind.Indicator, IndicatorCodes.CrimeRatePer1000, CriterionDirection.LowerIsBetter),
            new("vivienda", "Vivienda", CriterionSourceKind.Indicator, IndicatorCodes.AverageRent, CriterionDirection.LowerIsBetter),
            new("conectividad", "Conectividad", CriterionSourceKind.Indicator, IndicatorCodes.InternetHouseholdsPct, CriterionDirection.HigherIsBetter),
            new("educacion", "Educación", CriterionSourceKind.PoiLayer, "educacion", CriterionDirection.HigherIsBetter),
            new("salud", "Salud", CriterionSourceKind.PoiLayer, "salud", CriterionDirection.HigherIsBetter),
            new("espacios_verdes", "Espacios verdes", CriterionSourceKind.PoiLayer, "espacios_verdes", CriterionDirection.HigherIsBetter),
            new("comercios", "Comercios y servicios", CriterionSourceKind.PoiLayer, "comercios_servicios", CriterionDirection.HigherIsBetter),
            new("deporte", "Deporte", CriterionSourceKind.PoiLayer, "deporte", CriterionDirection.HigherIsBetter),
            new("cultura", "Cultura y gastronomía", CriterionSourceKind.PoiLayer, "cultura_gastronomia", CriterionDirection.HigherIsBetter),
        ];

        private static readonly Dictionary<string, CriterionDefinition> ByCode =
            All.ToDictionary(criterion => criterion.Code, StringComparer.Ordinal);

        public static CriterionDefinition Get(string code)
        {
            return ByCode.TryGetValue(code, out var criterion)
                ? criterion
                : throw new ArgumentException($"Criterio desconocido: '{code}'.", nameof(code));
        }
    }
}
