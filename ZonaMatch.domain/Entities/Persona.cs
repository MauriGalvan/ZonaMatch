namespace ZonaMatch.Domain.Entities
{
    // Integrante del hogar de un contexto (tabla app.personas), una fila por persona.
    public class Persona
    {
        public Guid Id { get; set; }

        public Guid ContextoId { get; set; }

        public Contexto? Contexto { get; set; }

        // Position in the profile wizard's household list.
        public int Orden { get; set; }

        // Display name entered in the profile wizard.
        public string? Nombre { get; set; }

        // Rango etario elegido en el wizard, verbatim (ej: "26–35 años"); null si no eligió
        public string? Edad { get; set; }
    }
}
