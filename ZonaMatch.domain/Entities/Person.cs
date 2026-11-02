namespace ZonaMatch.Domain.Entities
{
    // A member of a context's household (app.personas table), one row per person.
    public class Person
    {
        public Guid Id { get; set; }

        public Guid ContextoId { get; set; }

        public Context? Context { get; set; }

        // Position in the profile wizard's household list.
        public int Orden { get; set; }

        // Display name entered in the profile wizard.
        public string? Nombre { get; set; }

        // Age range selected in the wizard, stored verbatim (for example, "26–35 años"); null if omitted.
        public string? Edad { get; set; }
    }
}
