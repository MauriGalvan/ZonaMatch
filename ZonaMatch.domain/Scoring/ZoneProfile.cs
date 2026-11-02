namespace ZonaMatch.Domain.Scoring
{
    // Scoring data for a zone: census, crime, transit, amenities, rent, and sale prices.
    // The zone profile repository builds this data for every zone so each criterion can compare zones.
    public sealed class ZoneProfile
    {
        public string Slug { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;

        // OSM administrative level: 5 for communes and districts, 8 for localities, and 9 or 10 for neighborhoods.
        public int Level { get; init; }

        public double AreaKm2 { get; init; }
        public int Population { get; init; }

        // Estimated crimes of a year (geo.zona_indicadores.delitos_comparables): registered crimes corrected with the
        // victimization survey, so CABA and the province can be compared
        public int? CrimeCount { get; init; }

        // Public transit lines and departures during the morning peak period in this zone.
        public int TransitLineCount { get; init; }
        public int PeakHourDepartures { get; init; }

        // Schools and kindergartens, healthcare centers, and squares or parks (OpenStreetMap).
        public int SchoolCount { get; init; }
        public int HealthCenterCount { get; init; }
        public int GreenSpaceCount { get; init; }
        public double GreenSpaceAreaM2 { get; init; }

        // Zone center (WGS84), used as the origin for trips to the user's places.
        public double Latitude { get; init; }
        public double Longitude { get; init; }

        // Average rent in Argentine pesos and available properties; null when unavailable.
        public decimal? AverageRent { get; init; }
        public int? AvailableRentalProperties { get; init; }

        // Average monthly building expenses in Argentine pesos; null when unavailable.
        public decimal? AverageExpenses { get; init; }

        // Monthly housing cost: rent plus building expenses, or rent alone when expenses are unavailable.
        public decimal? MonthlyCost => AverageRent is { } rent ? rent + (AverageExpenses ?? 0) : null;

        // Average sale price in US dollars and properties for sale; null when unavailable.
        public decimal? AverageSalePriceUsd { get; init; }
        public int? AvailablePropertiesForSale { get; init; }

        // Rent in Argentine pesos and sale price in US dollars per square meter, across all housing types.
        public decimal? RentPricePerM2 { get; init; }
        public decimal? SalePricePerM2Usd { get; init; }

        // Separate apartment, house, and PH market data (keyed by HousingTypes); the fields above cover all types.
        public IReadOnlyDictionary<string, HousingMarket> MarketByPropertyType { get; init; } = new Dictionary<string, HousingMarket>();

        // Return the requested housing type's market, or the combined market when no type is specified. Houses and PHs
        // exclude building expenses because most do not pay them; those that do are in gated communities. An apartment
        // without its own expense data uses the zone average.
        public HousingMarket GetMarket(string? type)
        {
            if (!HousingTypes.IsValid(type))
                return new HousingMarket(
                    AverageRent, AvailableRentalProperties, AverageExpenses, AverageSalePriceUsd, AvailablePropertiesForSale, RentPricePerM2, SalePricePerM2Usd);

            var market = MarketByPropertyType.TryGetValue(type!, out var typeSpecificMarket) ? typeSpecificMarket : HousingMarket.NoData;
            return market with { AverageExpenses = HousingTypes.IncludesBuildingExpenses(type) ? market.AverageExpenses ?? AverageExpenses : 0 };
        }

        // Compare communes and districts with each other, and neighborhoods and localities with each other.
        public bool IsLargeZone => Level <= 5;

        // Criterion indicators where higher values are better. Null means there is no data to compare.

        public double? CrimesPerThousandResidents =>
            CrimeCount is { } crimeCount && Population > 0 ? crimeCount * 1000.0 / Population : null;

        public double? SchoolsPerTenThousandResidents => PerTenThousand(SchoolCount);

        public double? HealthCentersPerTenThousandResidents => PerTenThousand(HealthCenterCount);

        public double? GreenSpaceM2PerResident => Population > 0 ? GreenSpaceAreaM2 / Population : null;

        public double? PeakHourDeparturesPerKm2 => AreaKm2 > 0 ? PeakHourDepartures / AreaKm2 : null;

        private double? PerTenThousand(int count) => Population > 0 ? count * 10000.0 / Population : null;
    }
}
