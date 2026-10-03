using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Territory
{
    // valor de una variable (población, delitos, alquiler...) para una unidad, con su procedencia
    public class TerritorialIndicator
    {
        public TerritorialIndicator(
            long territorialUnitId,
            string indicatorCode,
            decimal value,
            string unit,
            short dataSourceId,
            DateOnly referenceDate)
        {
            TerritorialUnitId = Guard.Positive(territorialUnitId, nameof(territorialUnitId));
            IndicatorCode = Guard.NotBlank(indicatorCode, nameof(indicatorCode));
            Value = value;
            Unit = Guard.NotBlank(unit, nameof(unit));
            DataSourceId = Guard.Positive(dataSourceId, nameof(dataSourceId));
            ReferenceDate = referenceDate;
        }

        public long Id { get; private set; }
        public long TerritorialUnitId { get; private set; }
        public string IndicatorCode { get; private set; }
        public decimal Value { get; private set; }
        public string Unit { get; private set; }
        public short DataSourceId { get; private set; }
        public DateOnly ReferenceDate { get; private set; }
    }

    public static class IndicatorCodes
    {
        public const string Population = "population";
        public const string CrimeRatePer1000 = "crime_rate_per_1000";
        public const string AverageRent = "average_rent";
        public const string InternetHouseholdsPct = "internet_households_pct";
    }
}
