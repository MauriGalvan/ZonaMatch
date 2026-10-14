namespace ZonaMatch.Domain.Entities
{
    // Integrante del hogar de un contexto (tabla app.personas), una fila por persona.
    public class Persona
    {
        public Guid Id { get; set; }

        public Guid ContextoId { get; set; }

        public Contexto? Contexto { get; set; }

        // Posicion en la lista del wizard ("Persona 1", "Persona 2", ...)
        public int Orden { get; set; }

        // Nombre libre; el wizard actual no lo pide, queda null
        public string? Nombre { get; set; }

        // Rango etario elegido en el wizard, verbatim (ej: "26–35 años"); null si no eligió
        public string? Edad { get; set; }
    }
}
