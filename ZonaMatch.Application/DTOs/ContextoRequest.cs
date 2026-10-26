namespace ZonaMatch.Application.DTOs
{
    // Context block from the profile wizard: people, names, ages, mobility, and pets.
    public class ContextoRequest
    {
        public int Personas { get; set; } = 1;

        // Names are optional for older clients; missing names receive stable defaults.
        public List<string?> PersonNames { get; set; } = [];

        // Age range for each person, in order; fewer values than people are allowed.
        public List<string?> Edades { get; set; } = [];

        // Mobility options are stored as provided.
        public List<string> Movilidad { get; set; } = [];

        public List<string> Mascotas { get; set; } = [];
    }
}
