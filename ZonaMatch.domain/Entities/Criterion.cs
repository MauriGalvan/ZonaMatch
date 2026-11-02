namespace ZonaMatch.Domain.Entities
{
    // Catalog of criteria shown in the analysis wizard (app.criterios table).
    // Shared data that defines which criteria exist and how they are displayed.
    // User selections live in contexto_criterios (priority per analysis), punto_de_interes,
    // and the other normalized context tables.
    public class Criterion
    {
        // Slug used as a foreign key in contexto_criterios and as an API key ("seguridad", "movilidad", ...).
        public string Id { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        // Clase de icono de PrimeIcons, ej: "pi pi-shield"
        public string Icono { get; set; } = string.Empty;

        // Display order in the wizard.
        public int Orden { get; set; }

        // Hide a criterion from the wizard without deleting existing analyses that use it.
        public bool Activo { get; set; } = true;
    }
}
