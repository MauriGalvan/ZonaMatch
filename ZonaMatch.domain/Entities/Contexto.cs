namespace ZonaMatch.Domain.Entities
{
    // Criterio de busqueda de un usuario (tabla app.contextos): su hogar, la prioridad de cada
    // criterio, sus lugares y el tope de alquiler. Hay dos tipos (ver TipoContexto):
    // - perfil: el criterio base del usuario, uno por usuario, que usan todos sus analisis
    // - analisis: la copia con la que se calculo un analisis guardado (no cambia si despues se edita el perfil)
    // El detalle vive normalizado en personas, puntos_interes y contexto_criterios.
    public class Contexto
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }

        // perfil | analisis (TipoContexto)
        public string Tipo { get; set; } = TipoContexto.Perfil;

        // Cantidad de personas del hogar (1..8); los rangos etarios viven en Integrantes
        public int Personas { get; set; }

        // Mascotas marcadas: perro, gato, otras, ninguna (text[] como Resena.Temas)
        public string[]? Mascotas { get; set; }

        // Movilidad habitual: a-pie, transporte-publico, vehiculo, bici.
        // Igual que mascotas: una lista simple de lo que el usuario eligio, sin catalogo.
        public string[]? Movilidad { get; set; }

        // Tope de alquiler tipeado por el usuario, verbatim (ej: "800000"); null si no cargo
        public string? AlquilerMaximo { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }

        public DateTimeOffset FechaActualizacion { get; set; }

        // Filas de app.personas: una por integrante, con su rango etario
        public ICollection<Persona> Integrantes { get; set; } = new List<Persona>();

        // Filas de app.puntos_interes: los lugares a los que va el usuario
        public ICollection<PuntoDeInteres> Puntos { get; set; } = new List<PuntoDeInteres>();

        // Filas de app.contexto_criterios: criterio elegido + prioridad (baja|media|alta)
        public ICollection<ContextoCriterio> Criterios { get; set; } = new List<ContextoCriterio>();

        // Copia completa para guardar junto a un analisis: el analisis queda fijo aunque el perfil cambie
        public Contexto CopiarParaAnalisis(DateTimeOffset ahora) => new()
        {
            UsuarioId = UsuarioId,
            Tipo = TipoContexto.Analisis,
            Personas = Personas,
            Mascotas = Mascotas?.ToArray(),
            Movilidad = Movilidad?.ToArray(),
            AlquilerMaximo = AlquilerMaximo,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
            Integrantes = Integrantes
                .Select(p => new Persona { Orden = p.Orden, Nombre = p.Nombre, Edad = p.Edad })
                .ToList(),
            Puntos = Puntos
                .Select(p => new PuntoDeInteres
                {
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Direccion = p.Direccion,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Tipo = p.Tipo,
                    QuienVa = p.QuienVa,
                    Frecuencia = p.Frecuencia,
                    Prioridad = p.Prioridad,
                    TiempoMax = p.TiempoMax,
                })
                .ToList(),
            Criterios = Criterios
                .Select(c => new ContextoCriterio { CriterioId = c.CriterioId, Prioridad = c.Prioridad })
                .ToList(),
        };
    }

    // Valores de Contexto.Tipo
    public static class TipoContexto
    {
        public const string Perfil = "perfil";
        public const string Analisis = "analisis";
    }
}
