namespace ZonaMatch.Domain.Entities
{
    // Transporte publico de una zona en un dia habil (tablas geo.zona_transporte_*, las calcula
    // etl/calcular_transporte.sql a partir de los GTFS).

    // Paradas y lineas de un modo (colectivo, tren, subte) dentro de la zona
    public class ParadasModoZona
    {
        public string Modo { get; set; } = string.Empty;
        public int Paradas { get; set; }
        public int Lineas { get; set; }
    }

    // Salidas de una linea en una franja, en la parada de la zona por donde mas pasa
    public class SalidasLineaZona
    {
        public string Modo { get; set; } = string.Empty;
        public string Linea { get; set; } = string.Empty;
        public string Franja { get; set; } = string.Empty;
        public int Salidas { get; set; }
        public int Paradas { get; set; }
    }
}
