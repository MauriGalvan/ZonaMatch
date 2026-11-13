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

        // Share of the households that rent (census), 0 to 100; null without census data.
        public double? RentingHouseholdsPercentage { get; init; }

        // Partido or comuna the zone belongs to; null for partidos, comunas and the city themselves.
        public string? ParentSlug { get; init; }

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

        // Price distribution across all housing types (PriceDistribution): rent and rent per square meter in Argentine
        // pesos, sale prices in US dollars.
        public decimal[]? RentDeciles { get; init; }
        public decimal[]? RentPerM2Deciles { get; init; }
        public decimal[]? SaleDecilesUsd { get; init; }
        public decimal[]? SalePerM2DecilesUsd { get; init; }

        // Share of the listings priced up to each of those points (0 to 1), ties included.
        public decimal[]? RentShares { get; init; }
        public decimal[]? RentPerM2Shares { get; init; }
        public decimal[]? SaleShares { get; init; }
        public decimal[]? SalePerM2Shares { get; init; }

        // Sales eligible for a mortgage ("apto crédito") by property type (a HousingTypes value, or All) and rooms (0 for
        // every size, 1 to 5). Empty without credit data.
        public IReadOnlyDictionary<(string Type, int Rooms), CreditSales> CreditSalesBySegment { get; init; } =
            new Dictionary<(string Type, int Rooms), CreditSales>();

        // The credit-eligible sales closest to the search: its type and size, then its type in every size, then every
        // type in its size, and every type and size last. Null when the zone has none of them.
        public CreditSegment? FindCreditSales(string? type, int? rooms)
        {
            var requestedType = HousingTypes.IsValid(type) ? type! : HousingTypes.All;
            var requestedRooms = rooms is { } count && count >= HousingRooms.Minimum ? Math.Min(count, HousingRooms.Maximum) : 0;
            (string Type, int Rooms)[] candidates =
                [(requestedType, requestedRooms), (requestedType, 0), (HousingTypes.All, requestedRooms), (HousingTypes.All, 0)];

            foreach (var segment in candidates.Distinct())
            {
                if (CreditSalesBySegment.TryGetValue(segment, out var sales) && sales.HasData)
                    return new CreditSegment(segment.Type, segment.Rooms, sales);
            }

            return null;
        }

        // Separate apartment, house, and PH market data (keyed by HousingTypes); the fields above cover all types.
        public IReadOnlyDictionary<string, HousingMarket> MarketByPropertyType { get; init; } = new Dictionary<string, HousingMarket>();

        // Market data by number of rooms (keyed by a HousingTypes value, or All, and the rooms from 1 to 5).
        public IReadOnlyDictionary<(string Type, int Rooms), HousingMarket> MarketByRooms { get; init; } =
            new Dictionary<(string Type, int Rooms), HousingMarket>();

        // Return the requested housing type's market, or the combined market when no type is specified. Houses and PHs
        // exclude building expenses because most do not pay them; those that do are in gated communities. An apartment
        // without its own expense data uses the zone average.
        public HousingMarket GetMarket(string? type)
        {
            if (!HousingTypes.IsValid(type))
                return new HousingMarket(
                    AverageRent, AvailableRentalProperties, AverageExpenses, AverageSalePriceUsd, AvailablePropertiesForSale, RentPricePerM2, SalePricePerM2Usd,
                    RentDeciles: RentDeciles, RentPerM2Deciles: RentPerM2Deciles,
                    SaleDecilesUsd: SaleDecilesUsd, SalePerM2DecilesUsd: SalePerM2DecilesUsd,
                    RentShares: RentShares, RentPerM2Shares: RentPerM2Shares,
                    SaleShares: SaleShares, SalePerM2Shares: SalePerM2Shares);

            var market = MarketByPropertyType.TryGetValue(type!, out var typeSpecificMarket) ? typeSpecificMarket : HousingMarket.NoData;
            return market with { AverageExpenses = HousingTypes.IncludesBuildingExpenses(type) ? market.AverageExpenses ?? AverageExpenses : 0 };
        }

        // With a number of rooms, the prices of that size for the requested type, each operation on its own; where that
        // size has no data, the prices of every size (RentRooms or SaleRooms stay null).
        public HousingMarket GetMarket(string? type, int? rooms)
        {
            var general = GetMarket(type);
            if (rooms is not { } requestedRooms || requestedRooms < HousingRooms.Minimum)
                return general;

            var key = (HousingTypes.IsValid(type) ? type! : HousingTypes.All, Math.Min(requestedRooms, HousingRooms.Maximum));
            if (!MarketByRooms.TryGetValue(key, out var sized))
                return general;

            var rent = sized.AverageRent is not null;
            var sale = sized.AverageSalePriceUsd is not null;
            return general with
            {
                AverageRent = rent ? sized.AverageRent : general.AverageRent,
                AvailablePropertiesForRent = rent ? sized.AvailablePropertiesForRent : general.AvailablePropertiesForRent,
                RentPricePerM2 = rent ? sized.RentPricePerM2 : general.RentPricePerM2,
                // Houses and PHs stay without building expenses even when their size brings a value
                AverageExpenses = HousingTypes.IncludesBuildingExpenses(type)
                    ? (rent ? sized.AverageExpenses : null) ?? general.AverageExpenses
                    : general.AverageExpenses,
                AverageSalePriceUsd = sale ? sized.AverageSalePriceUsd : general.AverageSalePriceUsd,
                AvailablePropertiesForSale = sale ? sized.AvailablePropertiesForSale : general.AvailablePropertiesForSale,
                SalePricePerM2Usd = sale ? sized.SalePricePerM2Usd : general.SalePricePerM2Usd,
                RentRooms = rent ? key.Item2 : null,
                SaleRooms = sale ? key.Item2 : null,
                RentDeciles = rent ? sized.RentDeciles : general.RentDeciles,
                RentPerM2Deciles = rent ? sized.RentPerM2Deciles : general.RentPerM2Deciles,
                SaleDecilesUsd = sale ? sized.SaleDecilesUsd : general.SaleDecilesUsd,
                SalePerM2DecilesUsd = sale ? sized.SalePerM2DecilesUsd : general.SalePerM2DecilesUsd,
                RentShares = rent ? sized.RentShares : general.RentShares,
                RentPerM2Shares = rent ? sized.RentPerM2Shares : general.RentPerM2Shares,
                SaleShares = sale ? sized.SaleShares : general.SaleShares,
                SalePerM2Shares = sale ? sized.SalePerM2Shares : general.SalePerM2Shares,
            };
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
