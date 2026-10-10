namespace ZonaMatch.Domain.Entities
{
    // Resena de un vecino sobre una zona (tabla app.resenas). Una por usuario y zona; el autor puede editarla.
    public class Resena
    {
        public const int PuntajeMinimo = 1;
        public const int PuntajeMaximo = 5;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Slug de la zona, como en GET /Zonas/{slug}
        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        // Puntaje general y puntaje de cada aspecto (AspectoZona), de 1 a 5
        public int Puntaje { get; set; }
        public int PuntajeSeguridad { get; set; }
        public int PuntajeTransporte { get; set; }
        public int PuntajeConectividad { get; set; }
        public int PuntajeComercios { get; set; }
        public int PuntajeEspaciosVerdes { get; set; }

        public string Texto { get; set; } = string.Empty;

        // Cuanto hace que el autor vive en la zona, segun lo que declara (null: no lo dijo)
        public int? AniosEnZona { get; set; }

        // Codigos de AspectoZona de los que habla la resena, usados por los filtros de resenas
        public List<string> Temas { get; set; } = [];

        // Residencia verificada por la plataforma. Todavia no hay proceso de verificacion: siempre false.
        public bool Verificada { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset FechaActualizacion { get; set; }

        public List<ResenaVotoUtil> VotosUtil { get; set; } = [];
    }

    // Marca de "util" de un usuario en una resena (tabla app.resenas_votos_util)
    public class ResenaVotoUtil
    {
        public Guid ResenaId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
