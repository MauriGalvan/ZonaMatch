namespace ZonaMatch.Domain.Scoring
{
    // Scoring data for a zone: census, crime, transit, amenities, rent, and sale prices.
    // The zone profile repository builds this data for every zone so each criterion can compare zones.
    public sealed class ZoneProfile
    {
        public string Slug { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;

        // OSM administrative level: 5 for communes and districts, 8 for localities, and 9 or 10 for neighborhoods.
        public int Nivel { get; init; }

        public double SuperficieKm2 { get; init; }
        public int Poblacion { get; init; }

        // Estimated crimes of a year (geo.zona_indicadores.delitos_comparables): registered crimes corrected with the
        // victimization survey, so CABA and the province can be compared
        public int? Delitos { get; init; }

        // Public transit lines and departures during the morning peak period in this zone.
        public int LineasTransporte { get; init; }
        public int SalidasHoraPico { get; init; }

        // Schools and kindergartens, healthcare centers, and squares or parks (OpenStreetMap).
        public int Escuelas { get; init; }
        public int CentrosSalud { get; init; }
        public int EspaciosVerdes { get; init; }
        public double SuperficieVerdeM2 { get; init; }

        // Zone center (WGS84), used as the origin for trips to the user's places.
        public double Latitud { get; init; }
        public double Longitud { get; init; }

        // Average rent in Argentine pesos and available properties; null when unavailable.
        public decimal? AlquilerPromedio { get; init; }
        public int? PropiedadesDisponibles { get; init; }

        // Average monthly building expenses in Argentine pesos; null when unavailable.
        public decimal? ExpensasPromedio { get; init; }

        // Monthly housing cost: rent plus building expenses, or rent alone when expenses are unavailable.
        public decimal? CostoMensual => AlquilerPromedio is { } rent ? rent + (ExpensasPromedio ?? 0) : null;

        // Average sale price in US dollars and properties for sale; null when unavailable.
        public decimal? VentaPromedioUsd { get; init; }
        public int? PropiedadesEnVenta { get; init; }

        // Rent in Argentine pesos and sale price in US dollars per square meter, across all housing types.
        public decimal? AlquilerM2 { get; init; }
        public decimal? VentaM2Usd { get; init; }

        // Separate apartment, house, and PH market data (keyed by HousingTypes); the fields above cover all types.
        public IReadOnlyDictionary<string, HousingMarket> MercadoPorTipo { get; init; } = new Dictionary<string, HousingMarket>();

        // Return the requested housing type's market, or the combined market when no type is specified. Houses and PHs
        // exclude building expenses because most do not pay them; those that do are in gated communities. An apartment
        // without its own expense data uses the zone average.
        public HousingMarket GetMarket(string? type)
        {
            if (!HousingTypes.IsValid(type))
                return new HousingMarket(
                    AlquilerPromedio, PropiedadesDisponibles, ExpensasPromedio, VentaPromedioUsd, PropiedadesEnVenta, AlquilerM2, VentaM2Usd);

            var market = MercadoPorTipo.TryGetValue(type!, out var typeSpecificMarket) ? typeSpecificMarket : HousingMarket.NoData;
            return market with { ExpensasPromedio = HousingTypes.IncludesBuildingExpenses(type) ? market.ExpensasPromedio ?? ExpensasPromedio : 0 };
        }

        // Compare communes and districts with each other, and neighborhoods and localities with each other.
        public bool IsLargeZone => Nivel <= 5;

        // Criterion indicators where higher values are better. Null means there is no data to compare.

        public double? DelitosCadaMilHabitantes =>
            Delitos is { } delitos && Poblacion > 0 ? delitos * 1000.0 / Poblacion : null;

        public double? EscuelasCadaDiezMil => CadaDiezMil(Escuelas);

        public double? CentrosSaludCadaDiezMil => CadaDiezMil(CentrosSalud);

        public double? VerdePorHabitanteM2 => Poblacion > 0 ? SuperficieVerdeM2 / Poblacion : null;

        public double? SalidasHoraPicoPorKm2 => SuperficieKm2 > 0 ? SalidasHoraPico / SuperficieKm2 : null;

        private double? CadaDiezMil(int cantidad) => Poblacion > 0 ? cantidad * 10000.0 / Poblacion : null;
    }
}
