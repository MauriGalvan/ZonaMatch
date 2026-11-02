namespace ZonaMatch.Domain.Entities
{
    // Public transit in a zone on a weekday (geo.zona_transporte_* tables, populated by
    // etl/calcular_transporte.sql from GTFS data).

    // Stops and lines for one mode (existing codes: colectivo, tren, subte) in the zone.
    public class ZoneTransitModeStops
    {
        public string Modo { get; set; } = string.Empty;
        public int Paradas { get; set; }
        public int Lineas { get; set; }
    }

    // Departures for a line in one time window, measured at the zone stop with the most service.
    public class TransitLineDepartures
    {
        public string Modo { get; set; } = string.Empty;
        public string Linea { get; set; } = string.Empty;
        public string Franja { get; set; } = string.Empty;
        public int Salidas { get; set; }
        public int Paradas { get; set; }
    }
}
