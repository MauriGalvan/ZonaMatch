namespace ZonaMatch.Domain.Entities
{
    // A neighbor's review of a zone (table app.resenas). One per user and zone; the author can edit it.
    public class Resena
    {
        public const int PuntajeMinimo = 1;
        public const int PuntajeMaximo = 5;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Zone slug, as in GET /Zonas/{slug}
        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        // Overall rating and the rating of each aspect (AspectoZona), from 1 to 5
        public int Puntaje { get; set; }
        public int PuntajeSeguridad { get; set; }
        public int PuntajeTransporte { get; set; }
        public int PuntajeConectividad { get; set; }
        public int PuntajeComercios { get; set; }
        public int PuntajeEspaciosVerdes { get; set; }

        public string Texto { get; set; } = string.Empty;

        // How long the author has lived in the zone, as they state it (null: not said)
        public int? AniosEnZona { get; set; }

        // Codes of AspectoZona the review talks about, used by the review filters
        public List<string> Temas { get; set; } = [];

        // Residence checked by the platform. There is no verification process yet: always false.
        public bool Verificada { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset FechaActualizacion { get; set; }

        public List<ResenaVotoUtil> VotosUtil { get; set; } = [];
    }

    // "Useful" mark of a user on a review (table app.resenas_votos_util)
    public class ResenaVotoUtil
    {
        public Guid ResenaId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
