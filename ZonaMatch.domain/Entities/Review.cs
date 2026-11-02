namespace ZonaMatch.Domain.Entities
{
    // A community member's review of a zone (app.resenas table). One per user and zone; the author can edit it.
    public class Review
    {
        public const int PuntajeMinimo = 1;
        public const int PuntajeMaximo = 5;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Zone slug, as in GET /api/v1/zones/{slug}.
        public string ZonaSlug { get; set; } = string.Empty;

        public Guid UsuarioId { get; set; }
        public User? User { get; set; }

        // Score for each aspect (ReviewAspect), from 1 to 5.
        public int PuntajeSeguridad { get; set; }
        public int PuntajeTransporte { get; set; }
        public int PuntajeConectividad { get; set; }
        public int PuntajeComercios { get; set; }
        public int PuntajeEspaciosVerdes { get; set; }

        // Overall score: the unrounded average of the aspect scores. Not stored in the database.
        public double Puntaje =>
            (PuntajeSeguridad + PuntajeTransporte + PuntajeConectividad + PuntajeComercios + PuntajeEspaciosVerdes) / 5.0;

        public string Texto { get; set; } = string.Empty;

        // How long the author has lived in the zone, as reported by the author (null if not provided).
        public int? AniosEnZona { get; set; }

        // ReviewAspect codes mentioned in the review, used by review filters.
        public List<string> Temas { get; set; } = [];

        // Whether the platform verified the residence. No verification process exists yet, so this is always false.
        public bool Verificada { get; set; }

        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset FechaActualizacion { get; set; }

        public List<ReviewHelpfulVote> VotosUtil { get; set; } = [];
    }

    // A user's "helpful" vote on a review (app.resenas_votos_util table).
    public class ReviewHelpfulVote
    {
        public Guid ResenaId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
