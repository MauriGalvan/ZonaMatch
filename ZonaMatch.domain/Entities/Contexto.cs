namespace ZonaMatch.Domain.Entities
{
    // Cabecera de un analisis guardado (tabla app.contextos): reemplaza a la vieja
    // AnalisisDetallado, que guardaba el payload del wizard en tres columnas jsonb.
    // El payload ahora vive normalizado en personas, puntos_interes y las tablas intermedias.
    public class Contexto
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }

        // User-chosen name shown in the saved list, e.g. "Familia Palermo"
        public string Nombre { get; set; } = string.Empty;

        // Cantidad de personas del wizard (1..8); los rangos etarios viven en Integrantes
        public int Personas { get; set; }

        // Mascotas marcadas en el wizard: perro, gato, otras, ninguna (text[] como Resena.Temas)
        public string[]? Mascotas { get; set; }

        // Movilidad habitual marcada en el wizard: a-pie, transporte-publico, vehiculo, bici.
        // Igual que mascotas: una lista simple de lo que el usuario eligio, sin catalogo.
        public string[]? Movilidad { get; set; }

        // Tope de alquiler tipeado por el usuario, verbatim (ej: "800000"); null si no cargo
        public string? AlquilerMaximo { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }

        public DateTimeOffset FechaActualizacion { get; set; }

        // Filas de app.personas: una por integrante, con su rango etario
        public ICollection<Persona> Integrantes { get; set; } = new List<Persona>();

        // Filas de app.puntos_interes: lugares elegidos en el ultimo paso del wizard
        public ICollection<PuntoDeInteres> Puntos { get; set; } = new List<PuntoDeInteres>();

        // Filas de app.contexto_criterios: criterio elegido + prioridad (baja|media|alta)
        public ICollection<ContextoCriterio> Criterios { get; set; } = new List<ContextoCriterio>();
    }
}
