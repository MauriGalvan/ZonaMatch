namespace ZonaMatch.Domain.Entities
{
    // Datos abiertos de una zona (tabla geo.zona_indicadores, la calcula etl/calcular_zonas.sql): conteos del
    // Censo 2022 de los radios que caen en la zona y delitos del Mapa del Delito de CABA. Aca viven las reglas
    // que convierten esos conteos en indicadores (porcentajes, densidad, tasa de delitos).
    public class IndicadoresZona
    {
        public string Slug { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Nivel { get; set; }
        public double SuperficieKm2 { get; set; }

        // Censo 2022 (INDEC), por radio censal
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

        // Mapa del Delito: solo CABA tiene delitos con ubicacion. Null fuera de CABA (sin datos).
        public int? Delitos { get; set; }
        public int? Robos { get; set; }
        public int? Hurtos { get; set; }
        public int? Lesiones { get; set; }
        public int? Homicidios { get; set; }
        public int? Amenazas { get; set; }
        public int? SiniestrosViales { get; set; }

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

        // Delitos (sin siniestros viales) cada 1.000 habitantes en el anio
        public double? DelitosCada1000Habitantes =>
            Delitos is { } delitos && Poblacion > 0 ? Math.Round(1000.0 * delitos / Poblacion, 1) : null;

        // La edad se cuenta sobre las personas con edad informada, que pueden no ser toda la poblacion
        private int Poblacion0a65 => Personas0a14 + Personas15a64 + Personas65yMas;

        // Con un decimal; null cuando no hay base para calcularlo
        public static double? Porcentaje(int parte, int total) =>
            total > 0 ? Math.Round(100.0 * parte / total, 1) : null;
    }
}
