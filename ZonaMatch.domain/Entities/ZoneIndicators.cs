namespace ZonaMatch.Domain.Entities
{
    // Open data for a zone (geo.zona_indicadores table, populated by etl/calcular_zonas.sql): 2022 census counts
    // for census tracts in the zone and crime data (CABA Crime Map or SNIC district totals in the province).
    // This entity contains the rules that turn those counts into indicators (percentages, density, and crime rates).
    public class ZoneIndicators
    {
        public string Slug { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Nivel { get; set; }
        public double SuperficieKm2 { get; set; }

        // 2022 census data (INDEC), grouped by census tract.
        public int Radios { get; set; }
        public int Poblacion { get; set; }
        public int Viviendas { get; set; }
        public int Personas0a14 { get; set; }
        public int Personas15a64 { get; set; }
        public int Personas65yMas { get; set; }
        public int PersonasConCoberturaSalud { get; set; }
        public int PersonasConDatoCobertura { get; set; }
        public int PersonasUniversitario { get; set; }
        public int PersonasConDatoEducacion { get; set; }
        public int Hogares { get; set; }
        public int HogaresInternet { get; set; }
        public int HogaresCelularInternet { get; set; }
        public int HogaresComputadora { get; set; }
        public int HogaresNbi { get; set; }
        public int HogaresHacinamiento { get; set; }
        public int HogaresAlquilan { get; set; }

        // Crimes for the reported year. CABA uses geolocated CABA Crime Map events; the province uses SNIC district
        // counts distributed among localities by population. Null when data is unavailable.
        public int? Delitos { get; set; }
        public int? Robos { get; set; }
        public int? Hurtos { get; set; }
        public int? Lesiones { get; set; }
        public int? Homicidios { get; set; }
        public int? Amenazas { get; set; }
        public int? SiniestrosViales { get; set; }

        // FuenteMapaDelito or FuenteSnic source code.
        public string? DelitosFuente { get; set; }
        public int? DelitosPeriodo { get; set; }

        // SNIC districts used for the zone's crime data, ordered by population descending (for example, "Morón").
        public string? DelitosAmbito { get; set; }

        public const string FuenteMapaDelito = "mapa-delito";
        public const string FuenteSnic = "snic";

        // Geolocated crimes (CABA): the zone can be compared with the whole city
        public bool DelitosGeorreferenciados => DelitosFuente == FuenteMapaDelito;

        public bool TieneDelitos => Delitos is not null;

        public double? DensidadHabitantesKm2 =>
            SuperficieKm2 > 0 ? Math.Round(Poblacion / SuperficieKm2) : null;

        public double? PorcentajeMenores15 => Porcentaje(Personas0a14, Poblacion0a65);
        public double? Porcentaje15a64 => Porcentaje(Personas15a64, Poblacion0a65);
        public double? Porcentaje65yMas => Porcentaje(Personas65yMas, Poblacion0a65);

        public double? PorcentajeHogaresInternet => Porcentaje(HogaresInternet, Hogares);
        public double? PorcentajeHogaresCelularInternet => Porcentaje(HogaresCelularInternet, Hogares);
        public double? PorcentajeHogaresComputadora => Porcentaje(HogaresComputadora, Hogares);
        public double? PorcentajeHogaresNbi => Porcentaje(HogaresNbi, Hogares);
        public double? PorcentajeHogaresHacinamiento => Porcentaje(HogaresHacinamiento, Hogares);
        public double? PorcentajeHogaresAlquilan => Porcentaje(HogaresAlquilan, Hogares);

        public double? PorcentajeCoberturaSalud => Porcentaje(PersonasConCoberturaSalud, PersonasConDatoCobertura);
        public double? PorcentajeUniversitario => Porcentaje(PersonasUniversitario, PersonasConDatoEducacion);

        // Crimes per 1,000 residents for the year, excluding road incidents.
        public double? DelitosCada1000Habitantes =>
            Delitos is { } delitos && Poblacion > 0 ? Math.Round(1000.0 * delitos / Poblacion, 1) : null;

        // Age percentages use residents with a reported age, who may not represent the entire population.
        private int Poblacion0a65 => Personas0a14 + Personas15a64 + Personas65yMas;

        // One decimal place; null when there is no valid denominator.
        public static double? Porcentaje(int parte, int total) =>
            total > 0 ? Math.Round(100.0 * parte / total, 1) : null;
    }
}
