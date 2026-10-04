namespace ZonaMatch.Domain.Entities
{
    // Alternative municipality name mapped to a Partido (table geo.municipio_alias)
    public class MunicipioAlias
    {
        public string MunicipioNombre { get; set; } = string.Empty;
        public string PartidoKey { get; set; } = string.Empty;

        public Partido? Partido { get; set; }
    }
}
