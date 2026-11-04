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
        public int ResidentsAged0To14 { get; set; }
        public int ResidentsAged15To64 { get; set; }
        public int ResidentsAged65Plus { get; set; }
        public int ResidentsWithHealthCoverage { get; set; }
        public int ResidentsWithHealthCoverageData { get; set; }
        public int ResidentsWithUniversityEducation { get; set; }
        public int ResidentsWithEducationData { get; set; }
        public int Households { get; set; }
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

        public double? ResidentsUnder15Percentage => Percentage(ResidentsAged0To14, ResidentsWithKnownAge);
        public double? ResidentsAged15To64Percentage => Percentage(ResidentsAged15To64, ResidentsWithKnownAge);
        public double? ResidentsAged65PlusPercentage => Percentage(ResidentsAged65Plus, ResidentsWithKnownAge);

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

        // Age percentages use residents with a reported age, who may not represent the entire population.
        private int ResidentsWithKnownAge => ResidentsAged0To14 + ResidentsAged15To64 + ResidentsAged65Plus;

        // One decimal place; null when there is no valid denominator.
        public static double? Percentage(int numerator, int denominator) =>
            denominator > 0 ? Math.Round(100.0 * numerator / denominator, 1) : null;
    }
}
