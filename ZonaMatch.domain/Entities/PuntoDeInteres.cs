namespace ZonaMatch.Domain.Entities
{
    // Lugar elegido en el wizard (tabla app.puntos_interes), no confundir con los POI
    // geograficos del dump: este pertenece a un contexto y desaparece con el.
    public class PuntoDeInteres
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid ContextoId { get; set; }

        public Contexto? Contexto { get; set; }

        // Orden en que el usuario los cargó en el mapa del wizard
        public int Orden { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string? Direccion { get; set; }

        // Coordenadas al ubicar el lugar en el mapa; null si quedó sin ubicar
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? Tipo { get; set; }

        public string? QuienVa { get; set; }

        public string? Frecuencia { get; set; }

        public string? Prioridad { get; set; }

        public string? TiempoMax { get; set; }
    }
}
