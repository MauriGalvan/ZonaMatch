namespace ZonaMatch.Domain.Entities
{
    // A user's analysis criteria (app.contextos table): household, criterion priorities, places, and
    // rental or purchase limits. ContextType defines two types:
    // - perfil: the user's base criteria, one per user and shared by all their analyses.
    // - analisis: a snapshot used to calculate a saved analysis; profile edits do not change it.
    // Details are normalized across personas, puntos_interes, and contexto_criterios.
    public class Context
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UsuarioId { get; set; }

        // Existing type codes: perfil | analisis (ContextType).
        public string Tipo { get; set; } = ContextType.Perfil;

        // Number of people in the household (1..8); age ranges are stored in Person records.
        public int Personas { get; set; }

        // Selected pet codes (perro, gato, otras, ninguna), stored as a text array like Review.Temas.
        public string[]? Mascotas { get; set; }

        // Movilidad habitual: a-pie, transporte-publico, vehiculo, bici.
        // Like pets, a simple list of user-selected mobility options without a catalog.
        public string[]? Movilidad { get; set; }

        // User-entered monthly budget, stored verbatim (for example, "800000"); null if omitted.
        // Includes rent and expenses for renters, and expenses only for buyers.
        public string? AlquilerMaximo { get; set; }

        // Existing operation codes: alquiler | compra (Scoring.HousingOperations).
        public string Operacion { get; set; } = "alquiler";

        // Maximum purchase price in US dollars; used only when buying.
        public decimal? CompraMaximaUsd { get; set; }

        // apartment | house | ph (Scoring.HousingTypes); null means any type is acceptable.
        public string? TipoVivienda { get; set; }

        // Desired area in square meters; prices are estimated per square meter. Null uses zone averages.
        public int? SuperficieM2 { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }

        public DateTimeOffset FechaActualizacion { get; set; }

        // app.personas rows: one per household member, including their age range.
        public ICollection<Person> Integrantes { get; set; } = new List<Person>();

        // app.puntos_interes rows: places the user visits.
        public ICollection<ContextPlace> Puntos { get; set; } = new List<ContextPlace>();

        // app.contexto_criterios rows: selected criteria and their priorities (baja|media|alta).
        public ICollection<ContextCriterion> Criterios { get; set; } = new List<ContextCriterion>();

        // Create a full snapshot for a saved analysis so it remains unchanged if the profile changes.
        public Context CopiarParaAnalisis(DateTimeOffset ahora) => new()
        {
            UsuarioId = UsuarioId,
            Tipo = ContextType.Analysis,
            Personas = Personas,
            Mascotas = Mascotas?.ToArray(),
            Movilidad = Movilidad?.ToArray(),
            AlquilerMaximo = AlquilerMaximo,
            Operacion = Operacion,
            CompraMaximaUsd = CompraMaximaUsd,
            TipoVivienda = TipoVivienda,
            SuperficieM2 = SuperficieM2,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
            Integrantes = Integrantes
                .Select(p => new Person { Orden = p.Orden, Nombre = p.Nombre, Edad = p.Edad })
                .ToList(),
            Puntos = Puntos
                .Select(p => new ContextPlace
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
                .Select(c => new ContextCriterion { CriterioId = c.CriterioId, Prioridad = c.Prioridad })
                .ToList(),
        };
    }

    // Values used by Context.Type.
    public static class ContextType
    {
        public const string Perfil = "perfil";
        public const string Analysis = "analisis";
    }
}
