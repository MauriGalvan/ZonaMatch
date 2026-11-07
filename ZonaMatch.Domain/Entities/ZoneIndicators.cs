namespace ZonaMatch.Domain.Entities
{
    // Open data for a zone (geo.zona_indicadores table, populated by etl/calculate_zones.sql): 2022 census counts
    // for census tracts in the zone and crime data (CABA Crime Map or SNIC district totals in the province).
    // This entity contains the rules that turn those counts into indicators (percentages, density, and crime rates).
    public class ZoneIndicators
    {
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public double AreaKm2 { get; set; }

        // 2022 census data (INDEC), grouped by census tract.
        public int CensusTractCount { get; set; }
        public int Population { get; set; }
        public int Dwellings { get; set; }

        // Age brackets.
        public int ResidentsAged0To4 { get; set; }
        public int ResidentsAged5To14 { get; set; }
        public int ResidentsAged15To29 { get; set; }
        public int ResidentsAged30To44 { get; set; }
        public int ResidentsAged45To64 { get; set; }
        public int ResidentsAged65Plus { get; set; }

        // General age groups (retained for backward compatibility).
        public int ResidentsAged0To14 { get; set; }
        public int ResidentsAged15To64 { get; set; }

        public double? AverageAge { get; set; }

        public int ResidentsWithHealthCoverage { get; set; }
        public int ResidentsWithHealthCoverageData { get; set; }
        public int ResidentsWithUniversityEducation { get; set; }
        public int ResidentsWithEducationData { get; set; }

        public int Households { get; set; }
        public int SinglePersonHouseholds { get; set; }
        public int HouseholdsWithInternet { get; set; }
        public int HouseholdsWithMobileInternet { get; set; }
        public int HouseholdsWithComputer { get; set; }
        public int HouseholdsWithUnmetBasicNeeds { get; set; }
        public int OvercrowdedHouseholds { get; set; }
        public int RentingHouseholds { get; set; }

        // Crimes for the reported year. CABA uses geolocated CABA Crime Map events; the province uses SNIC district
        // counts distributed among localities by population. Null when data is unavailable.
        public int? CrimeCount { get; set; }
        public int? Robberies { get; set; }
        public int? Thefts { get; set; }
        public int? Injuries { get; set; }
        public int? Homicides { get; set; }
        public int? Threats { get; set; }
        public int? TrafficIncidents { get; set; }

        // CrimeMapSource or SnicSource code.
        public string? CrimeDataSource { get; set; }
        public int? CrimeDataYear { get; set; }

        // SNIC districts used for the zone's crime data, ordered by population descending (for example, "Morón").
        public string? CrimeDataCoverage { get; set; }

        public const string CrimeMapSource = "mapa-delito";
        public const string SnicSource = "snic";

        // Geolocated crimes (CABA): the zone can be compared with the whole city.
        public bool HasGeolocatedCrimes => CrimeDataSource == CrimeMapSource;

        public bool HasCrimeData => CrimeCount is not null;

        public double? PopulationDensityPerKm2 =>
            AreaKm2 > 0 ? Math.Round(Population / AreaKm2) : null;

        public double? AverageHouseholdSize =>
            Households > 0 ? Math.Round((double)Population / Households, 1) : null;

        public double? SinglePersonHouseholdsPercentage =>
            SinglePersonHouseholds > 0
                ? Percentage(SinglePersonHouseholds, Households)
                : (AverageHouseholdSize.HasValue
                    ? Math.Min(55.0, Math.Max(18.0, Math.Round((3.25 - AverageHouseholdSize.Value) * 35.5, 1)))
                    : null);

        // General group percentages (0-14, 15-64, 65+).
        public double? ResidentsUnder15Percentage => Percentage(
            ResidentsAged0To14 > 0 ? ResidentsAged0To14 : (ResidentsAged0To4 + ResidentsAged5To14),
            ResidentsWithKnownAge > 0 ? ResidentsWithKnownAge : AgeDenominator);

        public double? ResidentsAged15To64Percentage => Percentage(
            ResidentsAged15To64 > 0 ? ResidentsAged15To64 : (ResidentsAged15To29 + ResidentsAged30To44 + ResidentsAged45To64),
            ResidentsWithKnownAge > 0 ? ResidentsWithKnownAge : AgeDenominator);

        public double? ResidentsAged65PlusPercentage => Percentage(
            ResidentsAged65Plus,
            ResidentsWithKnownAge > 0 ? ResidentsWithKnownAge : AgeDenominator);

        // Detailed age distribution percentages over population (or estimated from general groups when not yet imported).
        public double? ResidentsAged0To4Percentage =>
            HasDetailedAges
                ? Percentage(ResidentsAged0To4, AgeDenominator)
                : (ResidentsUnder15Percentage.HasValue ? Math.Round(ResidentsUnder15Percentage.Value * 0.39, 1) : null);

        public double? ResidentsAged5To14Percentage =>
            HasDetailedAges
                ? Percentage(ResidentsAged5To14, AgeDenominator)
                : (ResidentsUnder15Percentage.HasValue ? Math.Round(ResidentsUnder15Percentage.Value * 0.61, 1) : null);

        public double? ResidentsAged15To29Percentage =>
            HasDetailedAges
                ? Percentage(ResidentsAged15To29, AgeDenominator)
                : (ResidentsAged15To64Percentage.HasValue ? Math.Round(ResidentsAged15To64Percentage.Value * 0.27, 1) : null);

        public double? ResidentsAged30To44Percentage =>
            HasDetailedAges
                ? Percentage(ResidentsAged30To44, AgeDenominator)
                : (ResidentsAged15To64Percentage.HasValue ? Math.Round(ResidentsAged15To64Percentage.Value * 0.51, 1) : null);

        public double? ResidentsAged45To64Percentage =>
            HasDetailedAges
                ? Percentage(ResidentsAged45To64, AgeDenominator)
                : (ResidentsAged15To64Percentage.HasValue ? Math.Round(ResidentsAged15To64Percentage.Value * 0.22, 1) : null);

        public double? ResidentsOver65Percentage =>
            ResidentsAged65PlusPercentage;

        public double? CalculatedAverageAge =>
            AverageAge ?? (ResidentsAged0To4Percentage.HasValue && ResidentsOver65Percentage.HasValue
                ? Math.Round(
                    ((ResidentsAged0To4Percentage.Value * 2.0) +
                     (ResidentsAged5To14Percentage!.Value * 9.5) +
                     (ResidentsAged15To29Percentage!.Value * 22.0) +
                     (ResidentsAged30To44Percentage!.Value * 37.0) +
                     (ResidentsAged45To64Percentage!.Value * 54.5) +
                     (ResidentsOver65Percentage.Value * 74.0)) / 100.0, 1)
                : null);

        public double? HouseholdsWithInternetPercentage => Percentage(HouseholdsWithInternet, Households);
        public double? HouseholdsWithMobileInternetPercentage => Percentage(HouseholdsWithMobileInternet, Households);
        public double? HouseholdsWithComputerPercentage => Percentage(HouseholdsWithComputer, Households);
        public double? HouseholdsWithUnmetBasicNeedsPercentage => Percentage(HouseholdsWithUnmetBasicNeeds, Households);
        public double? OvercrowdedHouseholdsPercentage => Percentage(OvercrowdedHouseholds, Households);
        public double? RentingHouseholdsPercentage => Percentage(RentingHouseholds, Households);

        public double? HealthCoveragePercentage => Percentage(ResidentsWithHealthCoverage, ResidentsWithHealthCoverageData);
        public double? UniversityEducationPercentage => Percentage(ResidentsWithUniversityEducation, ResidentsWithEducationData);

        // Crimes per 1,000 residents for the year, excluding road incidents.
        public double? CrimesPerThousandResidents =>
            CrimeCount is { } crimes && Population > 0 ? Math.Round(1000.0 * crimes / Population, 1) : null;

        public bool HasDetailedAges =>
            ResidentsAged0To4 > 0 || ResidentsAged5To14 > 0 || ResidentsAged15To29 > 0 ||
            ResidentsAged30To44 > 0 || ResidentsAged45To64 > 0;

        private int DetailedAgesTotal =>
            ResidentsAged0To4 + ResidentsAged5To14 + ResidentsAged15To29 + ResidentsAged30To44 + ResidentsAged45To64 + ResidentsAged65Plus;

        private int GeneralAgesTotal =>
            ResidentsAged0To14 + ResidentsAged15To64 + ResidentsAged65Plus;

        // Uses detailed total when detailed brackets are filled, otherwise general bands.
        private int ResidentsWithKnownAge =>
            HasDetailedAges ? DetailedAgesTotal : GeneralAgesTotal;

        private int AgeDenominator =>
            Population > 0 ? Population : (ResidentsWithKnownAge > 0 ? ResidentsWithKnownAge : 0);

        // One decimal place; null when there is no valid denominator.
        public static double? Percentage(int numerator, int denominator) =>
            denominator > 0 ? Math.Round(100.0 * numerator / denominator, 1) : null;
    }
}
