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

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // PrimeIcons CSS class, for example: "pi pi-shield".
        public string IconCssClass { get; set; } = string.Empty;

        // Display order in the wizard.
        public int Order { get; set; }

        // Hide a criterion from the wizard without deleting existing analyses that use it.
        public bool IsActive { get; set; } = true;

        // Profiles that use the criterion: the household one, the investor one, or both (safety and mobility).
        public bool ForHousehold { get; set; } = true;

        public bool ForInvestor { get; set; }
    }
}
