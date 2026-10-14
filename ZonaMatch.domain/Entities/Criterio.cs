namespace ZonaMatch.Domain.Entities
{
    // Catalogo de criterios del wizard de analisis (tabla app.criterios).
    // Es dato compartido por todos los usuarios: define que criterios existen y como se muestran.
    // Las elecciones del usuario viven en contexto_criterios (prioridad por analisis),
    // punto_de_interes y las demas tablas normalizadas del contexto.
    public class Criterio
    {
        // Slug que viaja como FK en contexto_criterios y como clave en la API ("seguridad", "movilidad", ...)
        public string Id { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        // Clase de icono de PrimeIcons, ej: "pi pi-shield"
        public string Icono { get; set; } = string.Empty;

        // Orden de aparicion en el wizard
        public int Orden { get; set; }

        // Un criterio desaparece del wizard sin borrar analisis que ya lo usan
        public bool Activo { get; set; } = true;
    }
}
