namespace ZonaMatch.Domain.Entities
{
    // Alquiler promedio y propiedades disponibles de una zona (tabla app.alquileres_zona).
    // Solucion temporal para el limite de alquiler del analisis: despues la llena el scraping de Argenprop.
    // Hay un registro por zona base (barrio o localidad); las comunas y los partidos se calculan sumando las
    // zonas que contienen.
    public class AlquilerZona
    {
        // Slug de la zona, igual que en GET /Zonas/{slug}
        public string ZonaSlug { get; set; } = string.Empty;

        // Alquiler mensual promedio, en pesos
        public decimal AlquilerPromedio { get; set; }

        public int PropiedadesDisponibles { get; set; }

        // De donde sale el dato: "estimado" (etl/estimar_alquileres.sql) o, mas adelante, "argenprop"
        public string Fuente { get; set; } = string.Empty;

        public DateTimeOffset FechaActualizacion { get; set; }
    }
}
