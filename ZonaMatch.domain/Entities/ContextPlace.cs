namespace ZonaMatch.Domain.Entities
{
    // A place selected in the wizard (app.puntos_interes table), not a geographic POI from the data dump.
    // This record belongs to a context and is deleted with it.
    public class ContextPlace
    {
        public Guid Id { get; set; }

        public Guid ContextoId { get; set; }

        public Context? Context { get; set; }

        // Order in which the user added places to the wizard map.
        public int Orden { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string? Direccion { get; set; }

        // Coordinates selected on the map; null if the place was not geocoded.
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? Tipo { get; set; }

        public string? QuienVa { get; set; }

        public string? Frecuencia { get; set; }

        public string? Prioridad { get; set; }

        public string? TiempoMax { get; set; }
    }
}
